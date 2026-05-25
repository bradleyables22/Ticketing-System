using Ticketing.Data.Models;

namespace Ticketing.Domain.Services;

public interface ITicketPermissionService
{
	Task<bool> CanViewTicketAsync(TicketRecord ticket, CancellationToken cancellationToken = default);

	Task<bool> CanWorkTicketAsync(TicketRecord ticket, CancellationToken cancellationToken = default);

	Task<bool> CanAssignTicketAsync(TicketRecord ticket, string? targetAssigneeOid, CancellationToken cancellationToken = default);

	Task<bool> CanAssignTicketTeamAsync(TicketRecord ticket, string? targetTeamId, CancellationToken cancellationToken = default);

	bool CanManageTeams();

	bool CanManageTaxonomy();

	bool IsTechnicianOrAbove();
}
