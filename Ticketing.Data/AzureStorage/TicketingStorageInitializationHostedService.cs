using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ticketing.Data.Configuration;
using Ticketing.Data.Stores;

namespace Ticketing.Data.AzureStorage;

internal sealed class TicketingStorageInitializationHostedService : IHostedService
{
	private readonly ITicketingStorageInitializer _initializer;
	private readonly TicketingDataOptions _options;
	private readonly ILogger<TicketingStorageInitializationHostedService> _logger;

	public TicketingStorageInitializationHostedService(
		ITicketingStorageInitializer initializer,
		IOptions<TicketingDataOptions> options,
		ILogger<TicketingStorageInitializationHostedService> logger)
	{
		_initializer = initializer;
		_options = options.Value;
		_logger = logger;
	}

	public async Task StartAsync(CancellationToken cancellationToken)
	{
		if (!_options.InitializeStorageOnStartup)
		{
			_logger.LogInformation("Ticketing storage initialization on startup is disabled.");
			return;
		}

		_logger.LogInformation("Initializing ticketing Azure Storage resources.");
		try
		{
			await _initializer.InitializeAsync(cancellationToken);
		}
		catch (Exception exception) when (IsAzuriteConnectionFailure(exception, _options.ConnectionString))
		{
			throw new InvalidOperationException(GetAzuriteConnectionFailureMessage(), exception);
		}

		_logger.LogInformation("Ticketing Azure Storage resources initialized.");
	}

	public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

	private static bool IsAzuriteConnectionFailure(Exception exception, string connectionString)
	{
		if (!connectionString.Contains("UseDevelopmentStorage=true", StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}

		return EnumerateExceptions(exception)
			.Select(current => current.Message)
			.Any(message =>
				message.Contains("actively refused", StringComparison.OrdinalIgnoreCase)
				|| message.Contains("127.0.0.1:10000", StringComparison.OrdinalIgnoreCase)
				|| message.Contains("127.0.0.1:10001", StringComparison.OrdinalIgnoreCase)
				|| message.Contains("127.0.0.1:10002", StringComparison.OrdinalIgnoreCase));
	}

	private static IEnumerable<Exception> EnumerateExceptions(Exception exception)
	{
		yield return exception;

		if (exception is AggregateException aggregateException)
		{
			foreach (var inner in aggregateException.Flatten().InnerExceptions)
			{
				foreach (var nested in EnumerateExceptions(inner))
				{
					yield return nested;
				}
			}

			yield break;
		}

		if (exception.InnerException is not null)
		{
			foreach (var inner in EnumerateExceptions(exception.InnerException))
			{
				yield return inner;
			}
		}
	}

	private static string GetAzuriteConnectionFailureMessage() =>
		"""
		Ticketing is configured with UseDevelopmentStorage=true, but Azurite is not reachable.

		Start Azurite before starting the API:

		    npm.cmd install -g azurite
		    azurite.cmd --location .\.azurite

		Or run the local helper from the repository root:

		    .\scripts\start-local.ps1

		Expected local Azurite endpoints:

		    Blob:  http://127.0.0.1:10000
		    Queue: http://127.0.0.1:10001
		    Table: http://127.0.0.1:10002
		""";
}
