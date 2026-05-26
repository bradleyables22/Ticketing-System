using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Ticketing.Rest.Endpoints;

public static class TicketingRestEndpointRouteBuilderExtensions
{
	public static IEndpointRouteBuilder MapTicketingRestApi(this IEndpointRouteBuilder endpoints)
	{
		var api = endpoints.MapGroup("/api")
			.RequireAuthorization();

		api.MapTicketEndpoints();
		api.MapTeamEndpoints();
		api.MapTaxonomyEndpoints();
		api.MapUserEndpoints();
		api.MapDashboardEndpoints();
		api.MapSystemEndpoints();

		return endpoints;
	}
}
