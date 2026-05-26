using Ticketing.Auth;
using Ticketing.Data.Models;
using Ticketing.Data.Stores;
using Ticketing.Domain.Exceptions;
using Ticketing.Domain.Models;

namespace Ticketing.Domain.Services;

internal sealed class TicketUserService : ITicketUserService
{
	private readonly CurrentUserService _currentUser;
	private readonly IUserProfileStore _userProfileStore;
	private readonly ITeamStore _teamStore;
	private readonly ITicketPermissionService _permissions;

	public TicketUserService(
		CurrentUserService currentUser,
		IUserProfileStore userProfileStore,
		ITeamStore teamStore,
		ITicketPermissionService permissions)
	{
		_currentUser = currentUser;
		_userProfileStore = userProfileStore;
		_teamStore = teamStore;
		_permissions = permissions;
	}

	public Task<DomainResult<TicketingCurrentUserContext>> GetCurrentAsync(CancellationToken cancellationToken = default) =>
		DomainResult<TicketingCurrentUserContext>.TryAsync(async () =>
		{
			var userOid = await _currentUser.RequireUserOidAndSyncProfileAsync(cancellationToken);
			var current = _currentUser.Current;
			var memberships = await _teamStore.GetMembershipsForUserAsync(userOid, false, null, cancellationToken)
				.ToReadOnlyListAsync(cancellationToken);

			return new TicketingCurrentUserContext
			{
				UserOid = userOid,
				TenantId = current.TenantId,
				DisplayName = string.IsNullOrWhiteSpace(current.DisplayName)
					? current.Email ?? userOid
					: current.DisplayName,
				Email = current.Email,
				Roles = current.Roles.Order(StringComparer.OrdinalIgnoreCase).ToArray(),
				Scopes = current.Scopes.Order(StringComparer.OrdinalIgnoreCase).ToArray(),
				TeamMemberships = memberships,
				Permissions = new TicketingPermissionSet
				{
					CanSubmitTickets = true,
					CanWorkTickets = _permissions.IsTechnicianOrAbove(),
					CanViewAllTickets = _permissions.CanViewAllTickets(),
					CanManageTeams = _permissions.CanManageTeams(),
					CanManageTaxonomy = _permissions.CanManageTaxonomy(),
					IsAdmin = current.IsInRole(TicketingAppRoles.Admin)
				}
			};
		});

	public Task<DomainResult<TicketUserProfile>> GetUserAsync(string userOid, CancellationToken cancellationToken = default) =>
		DomainResult<TicketUserProfile>.TryAsync(async () =>
		{
			ArgumentException.ThrowIfNullOrWhiteSpace(userOid);
			var currentUserOid = _currentUser.RequireUserOid();

			if (!_permissions.IsTechnicianOrAbove()
				&& !string.Equals(currentUserOid, userOid, StringComparison.OrdinalIgnoreCase))
			{
				throw new TicketingForbiddenException("Only technicians, managers, and admins can look up other users.");
			}

			return await _userProfileStore.GetAsync(userOid, cancellationToken)
				?? throw new TicketingNotFoundException("User", userOid);
		});

	public Task<DomainResult<IReadOnlyList<TicketUserProfile>>> SearchUsersAsync(
		string? query,
		bool includeInactive = false,
		int? pageSize = null,
		CancellationToken cancellationToken = default) =>
		DomainResult<IReadOnlyList<TicketUserProfile>>.TryAsync(async () =>
		{
			_currentUser.RequireUserOid();
			if (!_permissions.IsTechnicianOrAbove())
			{
				throw new TicketingForbiddenException("Only technicians, managers, and admins can search users.");
			}

			var canIncludeInactive = includeInactive && _permissions.CanManageTeams();
			return await _userProfileStore.SearchAsync(query, canIncludeInactive, NormalizePageSize(pageSize), cancellationToken)
				.ToReadOnlyListAsync(cancellationToken);
		});

	private static int NormalizePageSize(int? pageSize) =>
		Math.Clamp(pageSize.GetValueOrDefault(50), 1, 500);
}
