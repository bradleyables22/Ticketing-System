using Ticketing.Data.Models;

namespace Ticketing.Domain.Services;

internal interface ITicketPermissionService
{
	Task<bool> CanViewTicketAsync(TicketRecord ticket, CancellationToken cancellationToken = default);

	Task<bool> CanViewTicketSummaryAsync(TicketSummary ticket, CancellationToken cancellationToken = default);

	Task<bool> CanWorkTicketAsync(TicketRecord ticket, CancellationToken cancellationToken = default);

	Task<bool> CanWorkTicketSummaryAsync(TicketSummary ticket, CancellationToken cancellationToken = default);

	Task<bool> CanAssignTicketAsync(TicketRecord ticket, string? targetAssigneeOid, CancellationToken cancellationToken = default);

	Task<bool> CanAssignTicketTeamAsync(TicketRecord ticket, string? targetTeamId, CancellationToken cancellationToken = default);

	bool CanManageTeams();

	bool CanManageTaxonomy();

	bool CanViewAllTickets();

	bool IsTechnicianOrAbove();
}
