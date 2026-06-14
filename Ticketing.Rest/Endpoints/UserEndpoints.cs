using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Ticketing.Auth;
using Ticketing.Data.Models;
using Ticketing.Domain.Models;
using Ticketing.Domain.Services;
using Ticketing.Rest.Infrastructure;

namespace Ticketing.Rest.Endpoints;

internal static class UserEndpoints
{
	public static RouteGroupBuilder MapUserEndpoints(this RouteGroupBuilder api)
	{
		api.MapGet("/me", async (
				ITicketUserService ticketUsers,
				CancellationToken cancellationToken) =>
			{
				var result = await ticketUsers.GetCurrentAsync(cancellationToken);
				return DomainHttpResultMapper.ToResult(result);
			})
			.WithTags("Users")
			.RequireAuthorization(TicketingAuthPolicies.Read)
			.WithName("GetCurrentUser")
			.WithOkDocs<TicketingCurrentUserContext>(
				"Get current user context",
				"Returns the authenticated user's object id, tenant id, display name, email, roles, granted scopes, derived ticketing permissions, and team memberships.");

		var users = api.MapGroup("/users")
			.WithTags("Users")
			.RequireAuthorization(TicketingAuthPolicies.Read)
			.RequireAuthorization(TicketingAuthPolicies.WorkTicket);

		users.MapGet("/", async (
				string? query,
				bool includeInactive,
				int? pageSize,
				string? pageToken,
				ITicketUserService ticketUsers,
				CancellationToken cancellationToken) =>
			{
				var result = await ticketUsers.SearchUsersAsync(query, includeInactive, pageSize, pageToken, cancellationToken);
				return DomainHttpResultMapper.ToResult(result);
			})
			.WithName("SearchUsers")
			.WithOkDocs<PagedResult<TicketUserProfile>>(
				"Search users",
				"Searches users for assignment and administration workflows. When Microsoft Graph is configured this queries Graph and refreshes the local profile cache; otherwise it searches the local cache. Results are returned as a paged envelope.");

		users.MapGet("/{userOid}", async (
				string userOid,
				ITicketUserService ticketUsers,
				CancellationToken cancellationToken) =>
			{
				var result = await ticketUsers.GetUserAsync(userOid, cancellationToken);
				return DomainHttpResultMapper.ToResult(result);
			})
			.WithName("GetUser")
			.WithOkDocs<TicketUserProfile>(
				"Get a user profile",
				"Returns a cached ticketing user profile by Entra object id. Profiles are created from authenticated users and refreshed from Graph search when Graph is configured.",
				notFound: true);

		return users;
	}
}
