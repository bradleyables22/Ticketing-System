using Microsoft.Extensions.DependencyInjection;

namespace Ticketing.Rest.DependencyInjection;

public static class TicketingRestServiceCollectionExtensions
{
	public static IServiceCollection AddTicketingRest(this IServiceCollection services)
	{
		return services;
	}
}
