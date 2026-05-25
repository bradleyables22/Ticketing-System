using Microsoft.AspNetCore.Http;
using Ticketing.Auth.Claims;
using Ticketing.Auth.Models;

namespace Ticketing.Auth.Services;

internal sealed class HttpContextCurrentTicketingUserAccessor : ICurrentTicketingUserAccessor
{
	private readonly IHttpContextAccessor _httpContextAccessor;

	public HttpContextCurrentTicketingUserAccessor(IHttpContextAccessor httpContextAccessor)
	{
		_httpContextAccessor = httpContextAccessor;
	}

	public TicketingUser Current
	{
		get
		{
			var principal = _httpContextAccessor.HttpContext?.User;
			if (principal?.Identity?.IsAuthenticated != true)
			{
				return new TicketingUser();
			}

			return new TicketingUser
			{
				IsAuthenticated = true,
				UserOid = principal.GetTicketingUserOid(),
				TenantId = principal.GetTicketingTenantId(),
				DisplayName = principal.GetTicketingDisplayName(),
				Email = principal.GetTicketingEmail(),
				Roles = principal.GetTicketingRoles(),
				Scopes = principal.GetTicketingScopes()
			};
		}
	}
}
