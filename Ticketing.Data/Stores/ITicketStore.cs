using Ticketing.Data.Models;

namespace Ticketing.Data.Stores;

public interface ITicketStore
{
	Task<TicketRecord> CreateAsync(CreateTicketRequest request, CancellationToken cancellationToken = default);

	Task<TicketRecord?> GetAsync(string ticketId, CancellationToken cancellationToken = default);

	Task<TicketRecord?> GetByNumberAsync(string ticketNumber, CancellationToken cancellationToken = default);

	Task<TicketRecord> UpdateDetailsAsync(UpdateTicketDetailsRequest request, CancellationToken cancellationToken = default);

	Task<TicketRecord> AssignAsync(AssignTicketRequest request, CancellationToken cancellationToken = default);

	Task<TicketRecord> AssignTeamAsync(AssignTicketTeamRequest request, CancellationToken cancellationToken = default);

	Task<TicketRecord> CloseAsync(CloseTicketRequest request, CancellationToken cancellationToken = default);

	Task<TicketRecord> ReopenAsync(ReopenTicketRequest request, CancellationToken cancellationToken = default);
}
