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
		await _initializer.InitializeAsync(cancellationToken);
		_logger.LogInformation("Ticketing Azure Storage resources initialized.");
	}

	public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
