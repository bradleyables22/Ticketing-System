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
	private readonly IUserDirectoryStore? _userDirectoryStore;
	private readonly ITeamStore _teamStore;
	private readonly ITicketPermissionService _permissions;

	public TicketUserService(
		CurrentUserService currentUser,
		IUserProfileStore userProfileStore,
		IEnumerable<IUserDirectoryStore> userDirectoryStores,
		ITeamStore teamStore,
		ITicketPermissionService permissions)
	{
		_currentUser = currentUser;
		_userProfileStore = userProfileStore;
		_userDirectoryStore = userDirectoryStores.FirstOrDefault();
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
					CanSubmitTicketsOnBehalf = _permissions.IsTechnicianOrAbove(),
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

	public Task<DomainResult<PagedResult<TicketUserProfile>>> SearchUsersAsync(
		string? query,
		bool includeInactive = false,
		int? pageSize = null,
		string? pageToken = null,
		CancellationToken cancellationToken = default) =>
		DomainResult<PagedResult<TicketUserProfile>>.TryAsync(async () =>
		{
			_currentUser.RequireUserOid();
			if (!_permissions.IsTechnicianOrAbove())
			{
				throw new TicketingForbiddenException("Only technicians, managers, and admins can search users.");
			}

			var normalizedPageSize = DomainPaging.NormalizePageSize(pageSize);
			var canIncludeInactive = includeInactive && _permissions.CanManageTeams();
			if (_userDirectoryStore is not null)
			{
				var graphPage = await _userDirectoryStore.SearchUsersAsync(query, canIncludeInactive, normalizedPageSize, pageToken, cancellationToken);
				foreach (var profile in graphPage.Items)
				{
					await _userProfileStore.UpsertAsync(
						new UpsertUserProfileRequest
						{
							UserOid = profile.UserOid,
							DisplayName = profile.DisplayName,
							Email = profile.Email,
							Department = profile.Department,
							JobTitle = profile.JobTitle,
							IsActive = profile.IsActive
						},
						cancellationToken);
				}

				return graphPage;
			}

			return await _userProfileStore.SearchPageAsync(query, canIncludeInactive, normalizedPageSize, pageToken, cancellationToken);
		});
}
