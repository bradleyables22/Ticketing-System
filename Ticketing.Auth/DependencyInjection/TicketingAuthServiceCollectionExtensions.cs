using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Ticketing.Auth.Claims;
using Ticketing.Auth.Configuration;
using Ticketing.Auth.Services;

namespace Ticketing.Auth.DependencyInjection;

public static class TicketingAuthServiceCollectionExtensions
{
	public static IServiceCollection AddTicketingAuth(
		this IServiceCollection services,
		string tenantId,
		string clientId,
		Action<TicketingAuthOptions>? configure = null)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
		ArgumentException.ThrowIfNullOrWhiteSpace(clientId);

		var options = new TicketingAuthOptions
		{
			TenantId = tenantId.Trim(),
			ClientId = clientId.Trim()
		};

		configure?.Invoke(options);

		services.AddSingleton(Options.Create(options));
		services.AddHttpContextAccessor();
		services.AddScoped<ICurrentTicketingUserAccessor, HttpContextCurrentTicketingUserAccessor>();

		services
			.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
			.AddJwtBearer(jwt =>
			{
				jwt.Authority = options.Authority;
				jwt.Audience = options.ClientId;
				jwt.RequireHttpsMetadata = options.RequireHttpsMetadata;
				jwt.MapInboundClaims = false;
				jwt.TokenValidationParameters = new TokenValidationParameters
				{
					ValidateAudience = true,
					ValidAudiences = options.ValidAudiences,
					ValidateIssuer = true,
					NameClaimType = TicketingClaimTypes.Name,
					RoleClaimType = TicketingClaimTypes.Roles
				};
			});

		services.AddAuthorizationBuilder()
			.AddTicketingPolicies(options);

		return services;
	}

	private static AuthorizationBuilder AddTicketingPolicies(
		this AuthorizationBuilder builder,
		TicketingAuthOptions options)
	{
		builder.AddPolicy(TicketingAuthPolicies.SubmitTicket, policy =>
			policy.RequireAuthenticatedUser());

		builder.AddPolicy(TicketingAuthPolicies.ViewAllTickets, policy =>
			policy.RequireAuthenticatedUser()
				.RequireRole(
					options.Roles.Technician,
					options.Roles.Manager,
					options.Roles.Admin));

		builder.AddPolicy(TicketingAuthPolicies.WorkTicket, policy =>
			policy.RequireAuthenticatedUser()
				.RequireRole(
					options.Roles.Technician,
					options.Roles.Manager,
					options.Roles.Admin));

		builder.AddPolicy(TicketingAuthPolicies.ManageTaxonomy, policy =>
			policy.RequireAuthenticatedUser()
				.RequireRole(
					options.Roles.Manager,
					options.Roles.Admin));

		builder.AddPolicy(TicketingAuthPolicies.Admin, policy =>
			policy.RequireAuthenticatedUser()
				.RequireRole(options.Roles.Admin));

		return builder;
	}
}
