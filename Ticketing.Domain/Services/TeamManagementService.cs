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

	public Task<DomainResult<TeamRecord>> SaveTeamAsync(SaveTeamCommand command, CancellationToken cancellationToken = default) =>
		DomainResult<TeamRecord>.TryAsync(async () =>
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
		});

	public Task<DomainResult<TeamMemberRecord>> SaveMemberAsync(
		SaveTeamMemberCommand command,
		CancellationToken cancellationToken = default) =>
		DomainResult<TeamMemberRecord>.TryAsync(async () =>
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
		});

	public Task<DomainResult<TeamCategoryAssignmentRecord>> SaveCategoryAssignmentAsync(
		SaveTeamCategoryAssignmentCommand command,
		CancellationToken cancellationToken = default) =>
		DomainResult<TeamCategoryAssignmentRecord>.TryAsync(async () =>
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
		});

	public Task<DomainResult<TeamRecord>> GetTeamAsync(string teamId, CancellationToken cancellationToken = default) =>
		DomainResult<TeamRecord>.TryAsync(async () =>
		{
			ArgumentException.ThrowIfNullOrWhiteSpace(teamId);
			EnsureTechnicianOrAbove("Only technicians, managers, and admins can view teams.");

			return await _teamStore.GetTeamAsync(teamId, cancellationToken)
				?? throw new TicketingNotFoundException("Team", teamId);
		});

	public Task<DomainResult<IReadOnlyList<TeamRecord>>> GetTeamsAsync(
		bool includeInactive = false,
		int? pageSize = null,
		CancellationToken cancellationToken = default) =>
		DomainResult<IReadOnlyList<TeamRecord>>.TryAsync(async () =>
		{
			EnsureTechnicianOrAbove("Only technicians, managers, and admins can view teams.");
			return await _teamStore.GetTeamsAsync(CanIncludeInactive(includeInactive), pageSize, cancellationToken)
				.ToReadOnlyListAsync(cancellationToken);
		});

	public Task<DomainResult<IReadOnlyList<TeamMemberRecord>>> GetMyMembershipsAsync(
		bool includeInactive = false,
		int? pageSize = null,
		CancellationToken cancellationToken = default) =>
		DomainResult<IReadOnlyList<TeamMemberRecord>>.TryAsync(async () =>
		{
			var userOid = _currentUser.RequireUserOid();
			return await _teamStore.GetMembershipsForUserAsync(userOid, CanIncludeInactive(includeInactive), pageSize, cancellationToken)
				.ToReadOnlyListAsync(cancellationToken);
		});

	public Task<DomainResult<IReadOnlyList<TeamMemberRecord>>> GetMembersAsync(
		string teamId,
		bool includeInactive = false,
		int? pageSize = null,
		CancellationToken cancellationToken = default) =>
		DomainResult<IReadOnlyList<TeamMemberRecord>>.TryAsync(async () =>
		{
			ArgumentException.ThrowIfNullOrWhiteSpace(teamId);
			await EnsureCanViewTeamMembersAsync(teamId, cancellationToken);

			return await _teamStore.GetMembersAsync(teamId, CanIncludeInactive(includeInactive), pageSize, cancellationToken)
				.ToReadOnlyListAsync(cancellationToken);
		});

	public Task<DomainResult<IReadOnlyList<TeamCategoryAssignmentRecord>>> GetCategoryAssignmentsAsync(
		string? teamId = null,
		bool includeInactive = false,
		int? pageSize = null,
		CancellationToken cancellationToken = default) =>
		DomainResult<IReadOnlyList<TeamCategoryAssignmentRecord>>.TryAsync(async () =>
		{
			EnsureCanManageTeams();
			return await _teamStore.GetCategoryAssignmentsAsync(teamId, includeInactive, pageSize, cancellationToken)
				.ToReadOnlyListAsync(cancellationToken);
		});

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

	private async Task EnsureCanViewTeamMembersAsync(string teamId, CancellationToken cancellationToken)
	{
		var userOid = _currentUser.RequireUserOid();
		if (_permissions.CanManageTeams())
		{
			return;
		}

		if (!_permissions.IsTechnicianOrAbove()
			|| !await _teamStore.IsUserOnTeamAsync(userOid, teamId, cancellationToken))
		{
			throw new TicketingForbiddenException("You do not have permission to view this team's members.");
		}
	}

	private bool CanIncludeInactive(bool includeInactive) =>
		includeInactive && _permissions.CanManageTeams();
}
