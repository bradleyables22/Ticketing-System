using Microsoft.Extensions.DependencyInjection;
using Ticketing.Domain.Services;

namespace Ticketing.Domain.DependencyInjection;

public static class TicketingDomainServiceCollectionExtensions
{
	public static IServiceCollection AddTicketingDomain(this IServiceCollection services)
	{
		services.AddScoped<CurrentUserService>();
		services.AddScoped<ITicketPermissionService, TicketPermissionService>();
		services.AddScoped<ITicketWorkflowService, TicketWorkflowService>();
		services.AddScoped<ITeamManagementService, TeamManagementService>();
		services.AddScoped<ITaxonomyManagementService, TaxonomyManagementService>();
		services.AddScoped<ITicketUserService, TicketUserService>();

		return services;
	}
}
