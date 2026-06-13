using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Ticketing.Auth;
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
			.WithName("GetCurrentUser");

		var users = api.MapGroup("/users")
			.WithTags("Users")
			.RequireAuthorization(TicketingAuthPolicies.Read)
			.RequireAuthorization(TicketingAuthPolicies.WorkTicket);

		users.MapGet("/", async (
				string? query,
				bool includeInactive,
				int? pageSize,
				ITicketUserService ticketUsers,
				CancellationToken cancellationToken) =>
			{
				var result = await ticketUsers.SearchUsersAsync(query, includeInactive, pageSize, cancellationToken);
				return DomainHttpResultMapper.ToResult(result);
			})
			.WithName("SearchUsers");

		users.MapGet("/{userOid}", async (
				string userOid,
				ITicketUserService ticketUsers,
				CancellationToken cancellationToken) =>
			{
				var result = await ticketUsers.GetUserAsync(userOid, cancellationToken);
				return DomainHttpResultMapper.ToResult(result);
			})
			.WithName("GetUser");

		return users;
	}
}
