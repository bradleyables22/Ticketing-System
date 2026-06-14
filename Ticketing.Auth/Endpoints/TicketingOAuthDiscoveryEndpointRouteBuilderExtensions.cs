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
			.WithName("GetOAuthProtectedResourceMetadata")
			.WithSummary("Get OAuth protected resource metadata")
			.WithDescription("Returns RFC 9728-style protected resource metadata for OAuth-capable clients. Clients use this document to discover the resource identifier, authorization servers, bearer methods, and supported scopes.")
			.Produces<OAuthProtectedResourceMetadata>(StatusCodes.Status200OK)
			.Produces(StatusCodes.Status404NotFound);

		endpoints.MapGet("/.well-known/oauth-protected-resource/{**resourcePath}", GetProtectedResourceMetadata)
			.AllowAnonymous()
			.WithTags("OAuth")
			.WithName("GetOAuthProtectedResourceMetadataForResourcePath")
			.WithSummary("Get OAuth metadata for a resource path")
			.WithDescription("Returns protected resource metadata when the resource identifier includes a path segment. This supports clients that resolve metadata using the path-aware well-known convention.")
			.Produces<OAuthProtectedResourceMetadata>(StatusCodes.Status200OK)
			.Produces(StatusCodes.Status404NotFound);

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
