using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Ticketing.Auth.OAuth;

namespace Ticketing.Auth.Endpoints;

public static class TicketingOAuthDiscoveryEndpointRouteBuilderExtensions
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
	{
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
	};

	public static IEndpointRouteBuilder MapTicketingOAuthDiscovery(this IEndpointRouteBuilder endpoints)
	{
		endpoints.MapGet("/.well-known/oauth-protected-resource", GetProtectedResourceMetadata)
			.AllowAnonymous()
			.WithTags("OAuth")
			.WithName("GetOAuthProtectedResourceMetadata");

		endpoints.MapGet("/.well-known/oauth-protected-resource/{**resourcePath}", GetProtectedResourceMetadata)
			.AllowAnonymous()
			.WithTags("OAuth");

		return endpoints;
	}

	private static IResult GetProtectedResourceMetadata(
		HttpContext context,
		TicketingOAuthDiscoveryService discovery)
	{
		return discovery.IsEnabled
			? Results.Json(discovery.CreateMetadata(context.Request), JsonOptions)
			: Results.NotFound();
	}
}
