using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;
using Ticketing.Mcp.Configuration;
using Ticketing.Mcp.Infrastructure;

namespace Ticketing.Mcp.DependencyInjection;

public static class TicketingMcpServiceCollectionExtensions
{
	public static IServiceCollection AddTicketingMcp(
		this IServiceCollection services,
		Action<TicketingMcpOptions>? configure = null)
	{
		var options = new TicketingMcpOptions();
		configure?.Invoke(options);

		services.Replace(ServiceDescriptor.Singleton(Options.Create(options)));
		services.TryAddScoped<TicketingMcpAuthorizationService>();

		services
			.AddMcpServer()
			.WithHttpTransport()
			.WithToolsFromAssembly(typeof(TicketingMcpServiceCollectionExtensions).Assembly);

		return services;
	}
}
