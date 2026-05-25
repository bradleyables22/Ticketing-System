using Ticketing.Data.Models;
using Ticketing.Data.Stores;
using Ticketing.Domain.Exceptions;
using Ticketing.Domain.Models;

namespace Ticketing.Domain.Services;

internal sealed class TeamManagementService : ITeamManagementService
{
	private readonly CurrentUserService _currentUser;
	private readonly ITeamStore _teamStore;
	private readonly ITicketPermissionService _permissions;

	public TeamManagementService(
		CurrentUserService currentUser,
		ITeamStore teamStore,
		ITicketPermissionService permissions)
	{
		_currentUser = currentUser;
		_teamStore = teamStore;
		_permissions = permissions;
	}

	public async Task<TeamRecord> SaveTeamAsync(SaveTeamCommand command, CancellationToken cancellationToken = default)
	{
		EnsureCanManageTeams();
		var userOid = await _currentUser.RequireUserOidAndSyncProfileAsync(cancellationToken);

		return await _teamStore.SaveTeamAsync(
			new SaveTeamRequest
			{
				TeamId = command.TeamId,
				Name = command.Name,
				Description = command.Description,
				IsActive = command.IsActive,
				ActorOid = userOid
			},
			cancellationToken);
	}

	public async Task<TeamMemberRecord> SaveMemberAsync(
		SaveTeamMemberCommand command,
		CancellationToken cancellationToken = default)
	{
		EnsureCanManageTeams();
		var userOid = await _currentUser.RequireUserOidAndSyncProfileAsync(cancellationToken);

		return await _teamStore.SaveMemberAsync(
			new SaveTeamMemberRequest
			{
				TeamId = command.TeamId,
				UserOid = command.UserOid,
				Role = command.Role,
				IsActive = command.IsActive,
				ActorOid = userOid
			},
			cancellationToken);
	}

	public async Task<TeamCategoryAssignmentRecord> SaveCategoryAssignmentAsync(
		SaveTeamCategoryAssignmentCommand command,
		CancellationToken cancellationToken = default)
	{
		EnsureCanManageTeams();
		var userOid = await _currentUser.RequireUserOidAndSyncProfileAsync(cancellationToken);

		return await _teamStore.SaveCategoryAssignmentAsync(
			new SaveTeamCategoryAssignmentRequest
			{
				AssignmentId = command.AssignmentId,
				TeamId = command.TeamId,
				TypeId = command.TypeId,
				CategoryId = command.CategoryId,
				SubcategoryId = command.SubcategoryId,
				Priority = command.Priority,
				IsDefault = command.IsDefault,
				IsActive = command.IsActive,
				SortOrder = command.SortOrder,
				ActorOid = userOid
			},
			cancellationToken);
	}

	public IAsyncEnumerable<TeamRecord> GetTeamsAsync(
		bool includeInactive = false,
		int? pageSize = null,
		CancellationToken cancellationToken = default)
	{
		EnsureTechnicianOrAbove("Only technicians, managers, and admins can view teams.");
		return _teamStore.GetTeamsAsync(includeInactive, pageSize, cancellationToken);
	}

	public IAsyncEnumerable<TeamMemberRecord> GetMembersAsync(
		string teamId,
		bool includeInactive = false,
		int? pageSize = null,
		CancellationToken cancellationToken = default)
	{
		EnsureTechnicianOrAbove("Only technicians, managers, and admins can view team members.");
		return _teamStore.GetMembersAsync(teamId, includeInactive, pageSize, cancellationToken);
	}

	public IAsyncEnumerable<TeamCategoryAssignmentRecord> GetCategoryAssignmentsAsync(
		string? teamId = null,
		bool includeInactive = false,
		int? pageSize = null,
		CancellationToken cancellationToken = default)
	{
		EnsureCanManageTeams();
		return _teamStore.GetCategoryAssignmentsAsync(teamId, includeInactive, pageSize, cancellationToken);
	}

	private void EnsureCanManageTeams()
	{
		_currentUser.RequireUserOid();
		if (!_permissions.CanManageTeams())
		{
			throw new TicketingForbiddenException("Only managers and admins can manage teams.");
		}
	}

	private void EnsureTechnicianOrAbove(string message)
	{
		_currentUser.RequireUserOid();
		if (!_permissions.IsTechnicianOrAbove())
		{
			throw new TicketingForbiddenException(message);
		}
	}
}
