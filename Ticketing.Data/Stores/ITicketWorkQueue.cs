namespace Ticketing.Data.Stores;

public interface ITicketWorkQueue
{
	Task EnqueueProjectionRepairAsync(string ticketId, string reason, CancellationToken cancellationToken = default);

	Task EnqueueNotificationAsync(string ticketId, string eventName, CancellationToken cancellationToken = default);
}
