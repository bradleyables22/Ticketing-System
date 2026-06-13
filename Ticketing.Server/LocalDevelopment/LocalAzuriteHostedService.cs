using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Sockets;
using Microsoft.Extensions.Options;
using Ticketing.Data.Configuration;

namespace Ticketing.Server.LocalDevelopment;

internal sealed class LocalAzuriteHostedService : IHostedService
{
	private readonly IHostEnvironment _environment;
	private readonly TicketingDataOptions _dataOptions;
	private readonly LocalAzuriteOptions _options;
	private readonly ILogger<LocalAzuriteHostedService> _logger;
	private readonly ConcurrentQueue<string> _azuriteOutput = new();
	private readonly ConcurrentQueue<string> _azuriteError = new();
	private Process? _azuriteProcess;

	public LocalAzuriteHostedService(
		IHostEnvironment environment,
		IOptions<TicketingDataOptions> dataOptions,
		IOptions<LocalAzuriteOptions> options,
		ILogger<LocalAzuriteHostedService> logger)
	{
		_environment = environment;
		_dataOptions = dataOptions.Value;
		_options = options.Value;
		_logger = logger;
	}

	public async Task StartAsync(CancellationToken cancellationToken)
	{
		if (!_environment.IsDevelopment()
			|| !_options.Enabled
			|| !UsesDevelopmentStorage(_dataOptions.ConnectionString))
		{
			return;
		}

		var portStatus = await GetPortStatusAsync(cancellationToken);
		if (portStatus.IsReady)
		{
			_logger.LogInformation("Azurite is already listening on local development storage ports.");
			return;
		}

		if (portStatus.AnyOpen)
		{
			throw new InvalidOperationException(
				$"""
				Local development storage is partially listening, so the server will not start another Azurite instance.

				Port status:
				    Blob  {_options.BlobPort}: {(portStatus.BlobOpen ? "open" : "closed")}
				    Queue {_options.QueuePort}: {(portStatus.QueueOpen ? "open" : "closed")}
				    Table {_options.TablePort}: {(portStatus.TableOpen ? "open" : "closed")}

				Stop the existing Azurite/storage-emulator process and run the server again, or start full Azurite manually:

				    azurite.cmd --location .\.azurite
				""");
		}

		var command = FindAzuriteCommand()
			?? FindNpxCommand()
			?? throw new InvalidOperationException(
				"Azurite is required for local development storage but azurite/npx was not found. Install it with: npm.cmd install -g azurite");

		var arguments = GetAzuriteArguments(command.IsNpxFallback);

		_logger.LogInformation("Starting Azurite for local development storage.");
		_azuriteProcess = StartAzurite(command.Path, arguments);
		await WaitForAzuriteAsync(cancellationToken);
		_logger.LogInformation("Azurite is ready for local development storage.");
	}

	public Task StopAsync(CancellationToken cancellationToken)
	{
		if (_azuriteProcess is null
			|| _azuriteProcess.HasExited
			|| !_options.StopOnShutdown)
		{
			return Task.CompletedTask;
		}

		_logger.LogInformation("Stopping Azurite started by the ticketing server.");
		_azuriteProcess.Kill(entireProcessTree: true);
		return Task.CompletedTask;
	}

	private async Task WaitForAzuriteAsync(CancellationToken cancellationToken)
	{
		var timeout = TimeSpan.FromSeconds(Math.Max(1, _options.StartupTimeoutSeconds));
		var started = Stopwatch.StartNew();

		while (started.Elapsed < timeout)
		{
			cancellationToken.ThrowIfCancellationRequested();

			if (_azuriteProcess?.HasExited == true)
			{
				throw new InvalidOperationException(
					$"""
					Azurite exited before it was ready. Exit code: {_azuriteProcess.ExitCode}.

					Command output:
					{FormatProcessOutput(_azuriteOutput)}

					Command error:
					{FormatProcessOutput(_azuriteError)}
					""");
			}

			if (await IsAzuriteReadyAsync(cancellationToken))
			{
				return;
			}

			await Task.Delay(300, cancellationToken);
		}

		throw new TimeoutException(
			$"Timed out waiting for Azurite on ports {_options.BlobPort}, {_options.QueuePort}, and {_options.TablePort}.");
	}

	private async Task<bool> IsAzuriteReadyAsync(CancellationToken cancellationToken) =>
		(await GetPortStatusAsync(cancellationToken)).IsReady;

	private async Task<AzuritePortStatus> GetPortStatusAsync(CancellationToken cancellationToken) =>
		new(
			BlobOpen: await IsPortOpenAsync(_options.BlobPort, cancellationToken),
			QueueOpen: await IsPortOpenAsync(_options.QueuePort, cancellationToken),
			TableOpen: await IsPortOpenAsync(_options.TablePort, cancellationToken));

	private static async Task<bool> IsPortOpenAsync(int port, CancellationToken cancellationToken)
	{
		try
		{
			using var client = new TcpClient();
			var connectTask = client.ConnectAsync("127.0.0.1", port, cancellationToken).AsTask();
			var completedTask = await Task.WhenAny(connectTask, Task.Delay(300, cancellationToken));
			return completedTask == connectTask && client.Connected;
		}
		catch
		{
			return false;
		}
	}

	private Process StartAzurite(string commandPath, IReadOnlyList<string> arguments)
	{
		var startInfo = CreateStartInfo(commandPath, arguments);
		var process = Process.Start(startInfo)
			?? throw new InvalidOperationException("Failed to start Azurite.");

		process.OutputDataReceived += (_, args) =>
		{
			if (!string.IsNullOrWhiteSpace(args.Data))
			{
				_azuriteOutput.Enqueue(args.Data);
				_logger.LogDebug("azurite: {Message}", args.Data);
			}
		};
		process.ErrorDataReceived += (_, args) =>
		{
			if (!string.IsNullOrWhiteSpace(args.Data))
			{
				_azuriteError.Enqueue(args.Data);
				_logger.LogDebug("azurite: {Message}", args.Data);
			}
		};
		process.BeginOutputReadLine();
		process.BeginErrorReadLine();
		return process;
	}

	private string[] GetAzuriteArguments(bool useNpxFallback)
	{
		var arguments = new List<string>();
		if (useNpxFallback)
		{
			arguments.Add("-y");
			arguments.Add("azurite");
		}

		arguments.Add("--location");
		arguments.Add(ResolveDataPath());

		if (_options.SkipApiVersionCheck)
		{
			arguments.Add("--skipApiVersionCheck");
		}

		return [.. arguments];
	}

	private ProcessStartInfo CreateStartInfo(string commandPath, IReadOnlyList<string> arguments)
	{
		var dataPath = ResolveDataPath();
		Directory.CreateDirectory(dataPath);

		var startInfo = new ProcessStartInfo
		{
			UseShellExecute = false,
			RedirectStandardError = true,
			RedirectStandardOutput = true,
			CreateNoWindow = true,
			WorkingDirectory = _environment.ContentRootPath
		};

		if (OperatingSystem.IsWindows() && IsCommandScript(commandPath))
		{
			startInfo.FileName = Environment.GetEnvironmentVariable("COMSPEC") ?? "cmd.exe";
			startInfo.Arguments = $"/d /s /c \"{BuildWindowsCommand(commandPath, arguments)}\"";
		}
		else
		{
			startInfo.FileName = commandPath;
			foreach (var argument in arguments)
			{
				startInfo.ArgumentList.Add(argument);
			}
		}

		return startInfo;
	}

	private string ResolveDataPath()
	{
		return Path.GetFullPath(
			Path.IsPathRooted(_options.DataPath)
				? _options.DataPath
				: Path.Combine(_environment.ContentRootPath, _options.DataPath));
	}

	private LocalAzuriteCommand? FindAzuriteCommand()
	{
		var commandName = OperatingSystem.IsWindows() ? "azurite.cmd" : "azurite";
		return FindOnPath(commandName) is { } path
			? new LocalAzuriteCommand(path, IsNpxFallback: false)
			: null;
	}

	private LocalAzuriteCommand? FindNpxCommand()
	{
		if (!_options.UseNpxFallback)
		{
			return null;
		}

		var commandName = OperatingSystem.IsWindows() ? "npx.cmd" : "npx";
		return FindOnPath(commandName) is { } path
			? new LocalAzuriteCommand(path, IsNpxFallback: true)
			: null;
	}

	private static string? FindOnPath(string commandName)
	{
		var paths = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
			.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

		foreach (var path in paths)
		{
			var candidate = Path.Combine(path, commandName);
			if (File.Exists(candidate))
			{
				return candidate;
			}
		}

		return null;
	}

	private static bool UsesDevelopmentStorage(string connectionString) =>
		connectionString.Contains("UseDevelopmentStorage=true", StringComparison.OrdinalIgnoreCase);

	private static bool IsCommandScript(string commandPath) =>
		commandPath.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase)
		|| commandPath.EndsWith(".bat", StringComparison.OrdinalIgnoreCase);

	private static string BuildWindowsCommand(string commandPath, IEnumerable<string> arguments)
	{
		return string.Join(' ', new[] { commandPath }.Concat(arguments).Select(QuoteWindowsArgument));
	}

	private static string QuoteWindowsArgument(string argument)
	{
		return argument.Contains(' ', StringComparison.Ordinal)
			|| argument.Contains('"', StringComparison.Ordinal)
			? $"\"{argument.Replace("\"", "\\\"", StringComparison.Ordinal)}\""
			: argument;
	}

	private static string FormatProcessOutput(ConcurrentQueue<string> output)
	{
		var lines = output.ToArray();
		return lines.Length == 0
			? "(none)"
			: string.Join(Environment.NewLine, lines.TakeLast(40));
	}

	private sealed record LocalAzuriteCommand(string Path, bool IsNpxFallback);

	private sealed record AzuritePortStatus(bool BlobOpen, bool QueueOpen, bool TableOpen)
	{
		public bool IsReady => BlobOpen && QueueOpen && TableOpen;

		public bool AnyOpen => BlobOpen || QueueOpen || TableOpen;
	}
}
