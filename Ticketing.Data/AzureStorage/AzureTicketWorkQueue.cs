using System.Text.Json;
using Ticketing.Data.AzureStorage.Internal;
using Ticketing.Data.Stores;

namespace Ticketing.Data.AzureStorage;

internal sealed class AzureTicketWorkQueue : ITicketWorkQueue
{
	private readonly AzureStorageClients _clients;

	public AzureTicketWorkQueue(AzureStorageClients clients)
	{
		_clients = clients;
	}

	public Task EnqueueProjectionRepairAsync(string ticketId, string reason, CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(ticketId);
		ArgumentException.ThrowIfNullOrWhiteSpace(reason);

		return EnqueueAsync("projection-repair", ticketId, reason, cancellationToken);
	}

	private Task EnqueueAsync(string workType, string ticketId, string reason, CancellationToken cancellationToken)
	{
		var message = new TicketWorkQueueMessage(
			StorageKeys.NewId(),
			workType,
			ticketId,
			reason,
			DateTimeOffset.UtcNow);

		return _clients.WorkQueue.SendMessageAsync(JsonSerializer.Serialize(message), cancellationToken);
	}

	private sealed record TicketWorkQueueMessage(
		string WorkItemId,
		string WorkType,
		string TicketId,
		string Reason,
		DateTimeOffset CreatedUtc);
}
