using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Ticketing.Data.AzureStorage;
using Ticketing.Data.AzureStorage.Internal;
using Ticketing.Data.Configuration;
using Ticketing.Data.Graph;
using Ticketing.Data.Stores;

namespace Ticketing.Data.DependencyInjection;

public static class TicketingDataServiceCollectionExtensions
{
	public static IServiceCollection AddTicketingData(
		this IServiceCollection services,
		string azureStorageConnectionString,
		Action<TicketingDataOptions>? configure = null)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(azureStorageConnectionString);

		var options = new TicketingDataOptions
		{
			ConnectionString = azureStorageConnectionString
		};

		configure?.Invoke(options);

		services.AddSingleton(Options.Create(options));
		services.AddSingleton(_ => new TableServiceClient(options.ConnectionString));
		services.AddSingleton(_ => new BlobServiceClient(options.ConnectionString));
		services.AddSingleton(_ => new QueueServiceClient(
			options.ConnectionString,
			new QueueClientOptions { MessageEncoding = QueueMessageEncoding.Base64 }));

		services.AddSingleton<AzureStorageClients>();
		services.AddSingleton<TicketIndexProjector>();
		services.AddSingleton<TicketAuditWriter>();
		services.AddSingleton<TicketMutationService>();

		services.AddSingleton<ITicketingStorageInitializer, TicketingStorageInitializer>();
		services.AddSingleton<IHostedService, TicketingStorageInitializationHostedService>();
		services.AddSingleton<ITicketStore, AzureTicketStore>();
		services.AddSingleton<ITicketQueryStore, AzureTicketQueryStore>();
		services.AddSingleton<ITicketNoteStore, AzureTicketNoteStore>();
		services.AddSingleton<ITicketAttachmentStore, AzureTicketAttachmentStore>();
		services.AddSingleton<ITicketTaxonomyStore, AzureTicketTaxonomyStore>();
		services.AddSingleton<ITeamStore, AzureTeamStore>();
		services.AddSingleton<IUserProfileStore, AzureUserProfileStore>();
		services.AddSingleton<ITicketAuditStore, AzureTicketAuditStore>();
		services.AddSingleton<ITicketWorkQueue, AzureTicketWorkQueue>();
		services.AddSingleton<ITicketEmailNotificationQueue, AzureTicketEmailNotificationQueue>();

		return services;
	}

	public static IServiceCollection AddTicketingGraphUserDirectory(
		this IServiceCollection services,
		Action<TicketingGraphUserDirectoryOptions> configure)
	{
		var options = new TicketingGraphUserDirectoryOptions();
		configure(options);

		services.AddSingleton(Options.Create(options));
		if (options.Enabled)
		{
			services.AddSingleton<IUserDirectoryStore, GraphUserDirectoryStore>();
		}

		return services;
	}
}
