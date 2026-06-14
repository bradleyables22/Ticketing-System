using Azure.Storage.Blobs.Models;
using Ticketing.Data.AzureStorage.Internal;
using Ticketing.Data.Stores;

namespace Ticketing.Data.AzureStorage;

internal sealed class TicketingStorageInitializer : ITicketingStorageInitializer
{
	private readonly AzureStorageClients _clients;

	public TicketingStorageInitializer(AzureStorageClients clients)
	{
		_clients = clients;
	}

	public async Task InitializeAsync(CancellationToken cancellationToken = default)
	{
		foreach (var table in _clients.AllTables())
		{
			await table.CreateIfNotExistsAsync(cancellationToken);
		}

		await _clients.AttachmentsContainer.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken);
		await _clients.WorkQueue.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
		await _clients.EmailNotificationQueue.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
	}
}
