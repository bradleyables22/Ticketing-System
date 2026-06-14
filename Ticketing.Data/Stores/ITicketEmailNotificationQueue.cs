using Ticketing.Data.Models;

namespace Ticketing.Data.Stores;

public interface ITicketEmailNotificationQueue
{
	Task EnqueueAsync(
		QueueTicketEmailNotificationRequest request,
		CancellationToken cancellationToken = default);
}
