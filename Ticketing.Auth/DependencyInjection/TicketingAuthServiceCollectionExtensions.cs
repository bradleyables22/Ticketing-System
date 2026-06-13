using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Net.Http.Headers;
using Ticketing.Auth.Claims;
using Ticketing.Auth.Configuration;
using Ticketing.Auth.OAuth;
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
		services.TryAddSingleton<TicketingOAuthDiscoveryService>();
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
				jwt.Events = new JwtBearerEvents
				{
					OnChallenge = context =>
					{
						var discovery = context.HttpContext.RequestServices.GetRequiredService<TicketingOAuthDiscoveryService>();
						if (!discovery.ShouldAddChallenges)
						{
							return Task.CompletedTask;
						}

						context.HandleResponse();
						context.Response.StatusCode = StatusCodes.Status401Unauthorized;
						context.Response.Headers.Append(
							HeaderNames.WWWAuthenticate,
							discovery.CreateWwwAuthenticateChallenge(
								context.Request,
								context.Error,
								context.ErrorDescription,
								context.ErrorUri));

						return Task.CompletedTask;
					},
					OnForbidden = context =>
					{
						var discovery = context.HttpContext.RequestServices.GetRequiredService<TicketingOAuthDiscoveryService>();
						if (discovery.ShouldAddChallenges)
						{
							context.Response.Headers.Append(
								HeaderNames.WWWAuthenticate,
								discovery.CreateWwwAuthenticateChallenge(context.Request));
						}

						return Task.CompletedTask;
					}
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
		builder.AddPolicy(TicketingAuthPolicies.Read, policy =>
			policy.RequireAuthenticatedUser()
				.RequireAnyTicketingScope(
					options,
					options.Scopes.Read,
					options.Scopes.Write,
					options.Scopes.Manage,
					options.Scopes.System));

		builder.AddPolicy(TicketingAuthPolicies.Write, policy =>
			policy.RequireAuthenticatedUser()
				.RequireAnyTicketingScope(
					options,
					options.Scopes.Write,
					options.Scopes.Manage,
					options.Scopes.System));

		builder.AddPolicy(TicketingAuthPolicies.Manage, policy =>
			policy.RequireAuthenticatedUser()
				.RequireAnyTicketingScope(
					options,
					options.Scopes.Manage,
					options.Scopes.System));

		builder.AddPolicy(TicketingAuthPolicies.System, policy =>
			policy.RequireAuthenticatedUser()
				.RequireAnyTicketingScope(
					options,
					options.Scopes.System));

		builder.AddPolicy(TicketingAuthPolicies.SubmitTicket, policy =>
			policy.RequireAuthenticatedUser()
				.RequireAnyTicketingScope(
					options,
					options.Scopes.Write,
					options.Scopes.Manage,
					options.Scopes.System));

		builder.AddPolicy(TicketingAuthPolicies.ViewWorkQueues, policy =>
			policy.RequireAuthenticatedUser()
				.RequireAnyTicketingScope(
					options,
					options.Scopes.Read,
					options.Scopes.Write,
					options.Scopes.Manage,
					options.Scopes.System)
				.RequireRole(
					options.Roles.Technician,
					options.Roles.Manager,
					options.Roles.Admin));

		builder.AddPolicy(TicketingAuthPolicies.ViewAllTickets, policy =>
			policy.RequireAuthenticatedUser()
				.RequireAnyTicketingScope(
					options,
					options.Scopes.Read,
					options.Scopes.Write,
					options.Scopes.Manage,
					options.Scopes.System)
				.RequireRole(
					options.Roles.Manager,
					options.Roles.Admin));

		builder.AddPolicy(TicketingAuthPolicies.WorkTicket, policy =>
			policy.RequireAuthenticatedUser()
				.RequireRole(
					options.Roles.Technician,
					options.Roles.Manager,
					options.Roles.Admin));

		builder.AddPolicy(TicketingAuthPolicies.ManageTeams, policy =>
			policy.RequireAuthenticatedUser()
				.RequireAnyTicketingScope(
					options,
					options.Scopes.Manage,
					options.Scopes.System)
				.RequireRole(
					options.Roles.Manager,
					options.Roles.Admin));

		builder.AddPolicy(TicketingAuthPolicies.ManageTaxonomy, policy =>
			policy.RequireAuthenticatedUser()
				.RequireAnyTicketingScope(
					options,
					options.Scopes.Manage,
					options.Scopes.System)
				.RequireRole(
					options.Roles.Manager,
					options.Roles.Admin));

		builder.AddPolicy(TicketingAuthPolicies.Admin, policy =>
			policy.RequireAuthenticatedUser()
				.RequireAnyTicketingScope(
					options,
					options.Scopes.System)
				.RequireRole(options.Roles.Admin));

		return builder;
	}

	private static AuthorizationPolicyBuilder RequireAnyTicketingScope(
		this AuthorizationPolicyBuilder policy,
		TicketingAuthOptions options,
		params string[] acceptedScopes)
	{
		return policy.RequireAssertion(context =>
			!options.Scopes.RequireScopes
			|| context.User.HasAnyTicketingScope(acceptedScopes));
	}
}
