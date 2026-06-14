using Ticketing.Data.Models;

namespace Ticketing.Data.Stores;

public interface ITicketAuditStore
{
	IAsyncEnumerable<TicketAuditEventRecord> GetForTicketAsync(
		string ticketId,
		int? pageSize = null,
		CancellationToken cancellationToken = default);

	Task<PagedResult<TicketAuditEventRecord>> GetForTicketPageAsync(
		string ticketId,
		int? pageSize = null,
		string? pageToken = null,
		CancellationToken cancellationToken = default);
}
