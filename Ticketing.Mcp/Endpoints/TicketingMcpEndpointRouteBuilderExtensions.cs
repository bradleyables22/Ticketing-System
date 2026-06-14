using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ModelContextProtocol.AspNetCore;
using Ticketing.Mcp.Configuration;

namespace Ticketing.Mcp.Endpoints;

public static class TicketingMcpEndpointRouteBuilderExtensions
{
	public static IEndpointRouteBuilder MapTicketingMcp(this IEndpointRouteBuilder endpoints)
	{
		var options = endpoints.ServiceProvider
			.GetRequiredService<IOptions<TicketingMcpOptions>>()
			.Value;

		var mcp = endpoints.MapMcp(options.EndpointPath);
		if (options.RequireAuthorization)
		{
			if (string.IsNullOrWhiteSpace(options.AuthorizationPolicy))
			{
				mcp.RequireAuthorization();
			}
			else
			{
				mcp.RequireAuthorization(options.AuthorizationPolicy);
			}
		}

		return endpoints;
	}
}
