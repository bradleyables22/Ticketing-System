using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;
using Ticketing.Auth;
using Ticketing.Rest.Contracts;
using Ticketing.Rest.Infrastructure;

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
			.WithName("GetSystemInfo")
			.WithOkDocs<SystemInfoResponse>(
				"Get system information",
				"Returns basic host information for administrators, including application name, environment, assembly version, and current server UTC time.");

		return system;
	}
}
