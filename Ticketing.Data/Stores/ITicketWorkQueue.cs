namespace Ticketing.Data.Stores;

public interface ITicketWorkQueue
{
	Task EnqueueProjectionRepairAsync(string ticketId, string reason, CancellationToken cancellationToken = default);
}
