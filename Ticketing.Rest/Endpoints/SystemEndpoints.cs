using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;
using Ticketing.Auth;
using Ticketing.Rest.Contracts;

namespace Ticketing.Rest.Endpoints;

internal static class SystemEndpoints
{
	public static RouteGroupBuilder MapSystemEndpoints(this RouteGroupBuilder api)
	{
		var system = api.MapGroup("/system")
			.WithTags("System")
			.RequireAuthorization(TicketingAuthPolicies.Admin);

		system.MapGet("/info", (IHostEnvironment environment) =>
			Results.Ok(new SystemInfoResponse
			{
				ApplicationName = environment.ApplicationName,
				EnvironmentName = environment.EnvironmentName,
				Version = Assembly.GetEntryAssembly()?.GetName().Version?.ToString(),
				ServerTimeUtc = DateTimeOffset.UtcNow
			}))
			.WithName("GetSystemInfo");

		return system;
	}
}
