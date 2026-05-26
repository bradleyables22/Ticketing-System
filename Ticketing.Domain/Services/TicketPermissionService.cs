using Ticketing.Auth;
using Ticketing.Data.Models;
using Ticketing.Data.Stores;

namespace Ticketing.Domain.Services;

internal sealed class TicketPermissionService : ITicketPermissionService
{
	private readonly CurrentUserService _currentUser;
	private readonly ITeamStore _teamStore;

	public TicketPermissionService(CurrentUserService currentUser, ITeamStore teamStore)
	{
		_currentUser = currentUser;
		_teamStore = teamStore;
	}

	public async Task<bool> CanViewTicketAsync(TicketRecord ticket, CancellationToken cancellationToken = default)
	{
		var userOid = _currentUser.RequireUserOid();

		if (IsManagerOrAdmin()
			|| string.Equals(ticket.SubmitterOid, userOid, StringComparison.OrdinalIgnoreCase)
			|| string.Equals(ticket.AssigneeOid, userOid, StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}

		return IsTechnicianOrAbove()
			&& !string.IsNullOrWhiteSpace(ticket.AssignedTeamId)
			&& await _teamStore.IsUserOnTeamAsync(userOid, ticket.AssignedTeamId, cancellationToken);
	}

	public async Task<bool> CanViewTicketSummaryAsync(TicketSummary ticket, CancellationToken cancellationToken = default)
	{
		var userOid = _currentUser.RequireUserOid();

		if (CanViewAllTickets()
			|| string.Equals(ticket.SubmitterOid, userOid, StringComparison.OrdinalIgnoreCase)
			|| string.Equals(ticket.AssigneeOid, userOid, StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}

		return IsTechnicianOrAbove()
			&& !string.IsNullOrWhiteSpace(ticket.AssignedTeamId)
			&& await _teamStore.IsUserOnTeamAsync(userOid, ticket.AssignedTeamId, cancellationToken);
	}

	public async Task<bool> CanWorkTicketAsync(TicketRecord ticket, CancellationToken cancellationToken = default)
	{
		var userOid = _currentUser.RequireUserOid();

		if (IsManagerOrAdmin()
			|| string.Equals(ticket.AssigneeOid, userOid, StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}

		return IsTechnicianOrAbove()
			&& !string.IsNullOrWhiteSpace(ticket.AssignedTeamId)
			&& await _teamStore.IsUserOnTeamAsync(userOid, ticket.AssignedTeamId, cancellationToken);
	}

	public async Task<bool> CanWorkTicketSummaryAsync(TicketSummary ticket, CancellationToken cancellationToken = default)
	{
		var userOid = _currentUser.RequireUserOid();

		if (CanViewAllTickets()
			|| string.Equals(ticket.AssigneeOid, userOid, StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}

		return IsTechnicianOrAbove()
			&& !string.IsNullOrWhiteSpace(ticket.AssignedTeamId)
			&& await _teamStore.IsUserOnTeamAsync(userOid, ticket.AssignedTeamId, cancellationToken);
	}

	public async Task<bool> CanAssignTicketAsync(
		TicketRecord ticket,
		string? targetAssigneeOid,
		CancellationToken cancellationToken = default)
	{
		var userOid = _currentUser.RequireUserOid();

		if (IsManagerOrAdmin())
		{
			return true;
		}

		if (!IsTechnicianOrAbove() || string.IsNullOrWhiteSpace(ticket.AssignedTeamId))
		{
			return false;
		}

		if (!await _teamStore.IsUserOnTeamAsync(userOid, ticket.AssignedTeamId, cancellationToken))
		{
			return false;
		}

		if (string.IsNullOrWhiteSpace(targetAssigneeOid))
		{
			return await _teamStore.IsUserTeamLeadAsync(userOid, ticket.AssignedTeamId, cancellationToken)
				|| string.Equals(ticket.AssigneeOid, userOid, StringComparison.OrdinalIgnoreCase);
		}

		if (string.Equals(targetAssigneeOid, userOid, StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}

		return await _teamStore.IsUserTeamLeadAsync(userOid, ticket.AssignedTeamId, cancellationToken)
			&& await _teamStore.IsUserOnTeamAsync(targetAssigneeOid, ticket.AssignedTeamId, cancellationToken);
	}

	public Task<bool> CanAssignTicketTeamAsync(
		TicketRecord ticket,
		string? targetTeamId,
		CancellationToken cancellationToken = default) =>
		Task.FromResult(IsManagerOrAdmin());

	public bool CanManageTeams() => IsManagerOrAdmin();

	public bool CanManageTaxonomy() => IsManagerOrAdmin();

	public bool CanViewAllTickets() => IsManagerOrAdmin();

	public bool IsTechnicianOrAbove() =>
		HasRole(TicketingAppRoles.Technician) || IsManagerOrAdmin();

	private bool IsManagerOrAdmin() =>
		HasRole(TicketingAppRoles.Manager) || HasRole(TicketingAppRoles.Admin);

	private bool HasRole(string role) => _currentUser.Current.IsInRole(role);
}
