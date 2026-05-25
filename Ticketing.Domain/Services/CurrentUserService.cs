using Ticketing.Auth.Models;
using Ticketing.Auth.Services;
using Ticketing.Data.Models;
using Ticketing.Data.Stores;
using Ticketing.Domain.Exceptions;

namespace Ticketing.Domain.Services;

internal sealed class CurrentUserService
{
	private readonly ICurrentTicketingUserAccessor _currentUserAccessor;
	private readonly IUserProfileStore _userProfileStore;

	public CurrentUserService(
		ICurrentTicketingUserAccessor currentUserAccessor,
		IUserProfileStore userProfileStore)
	{
		_currentUserAccessor = currentUserAccessor;
		_userProfileStore = userProfileStore;
	}

	public TicketingUser Current => _currentUserAccessor.Current;

	public string RequireUserOid()
	{
		var current = Current;
		if (!current.IsAuthenticated)
		{
			throw new TicketingAuthenticationRequiredException();
		}

		return current.RequireUserOid();
	}

	public async Task<string> RequireUserOidAndSyncProfileAsync(CancellationToken cancellationToken)
	{
		var userOid = RequireUserOid();
		var current = Current;

		await _userProfileStore.UpsertAsync(
			new UpsertUserProfileRequest
			{
				UserOid = userOid,
				DisplayName = string.IsNullOrWhiteSpace(current.DisplayName) ? current.Email ?? userOid : current.DisplayName,
				Email = current.Email,
				IsActive = true
			},
			cancellationToken);

		return userOid;
	}
}
