using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Ticketing.Auth;
using Ticketing.Domain.Services;
using Ticketing.Rest.Infrastructure;

namespace Ticketing.Rest.Endpoints;

internal static class DashboardEndpoints
{
	public static RouteGroupBuilder MapDashboardEndpoints(this RouteGroupBuilder api)
	{
		var dashboard = api.MapGroup("/dashboard")
			.WithTags("Dashboard")
			.RequireAuthorization(TicketingAuthPolicies.Read);

		dashboard.MapGet("/summary", async (
				string? teamId,
				ITicketDashboardService ticketDashboard,
				CancellationToken cancellationToken) =>
			{
				var result = await ticketDashboard.GetSummaryAsync(teamId, cancellationToken);
				return DomainHttpResultMapper.ToResult(result);
			})
			.WithName("GetDashboardSummary");

		return dashboard;
	}
}
