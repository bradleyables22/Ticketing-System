using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Ticketing.Domain.Configuration;
using Ticketing.Domain.Services;

namespace Ticketing.Domain.DependencyInjection;

public static class TicketingDomainServiceCollectionExtensions
{
	public static IServiceCollection AddTicketingDomain(this IServiceCollection services)
	{
		services.TryAddSingleton(new TicketAttachmentUploadOptions());
		return AddTicketingDomainServices(services);
	}

	public static IServiceCollection AddTicketingDomain(
		this IServiceCollection services,
		Action<TicketAttachmentUploadOptions> configureAttachmentUploads)
	{
		ArgumentNullException.ThrowIfNull(configureAttachmentUploads);

		var options = new TicketAttachmentUploadOptions();
		configureAttachmentUploads(options);
		services.AddSingleton(options);

		return AddTicketingDomainServices(services);
	}

	private static IServiceCollection AddTicketingDomainServices(IServiceCollection services)
	{
		services.AddScoped<CurrentUserService>();
		services.AddScoped<ITicketPermissionService, TicketPermissionService>();
		services.AddScoped<TicketAttachmentUploadValidator>();
		services.AddScoped<ITicketWorkflowService, TicketWorkflowService>();
		services.AddScoped<ITeamManagementService, TeamManagementService>();
		services.AddScoped<ITaxonomyManagementService, TaxonomyManagementService>();
		services.AddScoped<ITicketUserService, TicketUserService>();

		return services;
	}
}
