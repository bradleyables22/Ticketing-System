using Ticketing.Data.Models;

namespace Ticketing.Data.Stores;

public interface ITicketNoteStore
{
	Task<TicketNoteRecord> AddAsync(AddTicketNoteRequest request, CancellationToken cancellationToken = default);

	IAsyncEnumerable<TicketNoteRecord> GetForTicketAsync(
		string ticketId,
		bool includeInternal,
		int? pageSize = null,
		CancellationToken cancellationToken = default);
}
