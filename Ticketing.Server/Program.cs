using Ticketing.Auth.Configuration;
using Ticketing.Auth.DependencyInjection;
using Ticketing.Auth.Endpoints;
using Ticketing.Data.DependencyInjection;
using Ticketing.Domain.DependencyInjection;
using Ticketing.Rest.DependencyInjection;
using Ticketing.Rest.Endpoints;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.OpenApi;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var azureAdTenantId =
	builder.Configuration["TICKETING_AUTH_TENANT_ID"]
	?? builder.Configuration["AzureAd:TenantId"]
	?? throw new InvalidOperationException(
		"Azure AD tenant id is required. Set TICKETING_AUTH_TENANT_ID or AzureAd__TenantId.");

var azureAdClientId =
	builder.Configuration["TICKETING_AUTH_CLIENT_ID"]
	?? builder.Configuration["AzureAd:ClientId"]
	?? throw new InvalidOperationException(
		"Azure AD client id is required. Set TICKETING_AUTH_CLIENT_ID or AzureAd__ClientId.");

var azureStorageConnectionString =
	builder.Configuration["TICKETING_AZURE_STORAGE_CONNECTION_STRING"]
	?? builder.Configuration.GetConnectionString("AzureStorage")
	?? throw new InvalidOperationException(
		"Azure Storage connection string is required. Set TICKETING_AZURE_STORAGE_CONNECTION_STRING or ConnectionStrings__AzureStorage.");

builder.Services.AddTicketingAuth(
	azureAdTenantId,
	azureAdClientId,
	options =>
	{
		options.Instance = builder.Configuration["TICKETING_AUTH_INSTANCE"] ?? options.Instance;
		ConfigureRoles(options.Roles, builder.Configuration);
		ConfigureScopes(options.Scopes, builder.Configuration);
		ConfigureOAuthDiscovery(options.OAuthDiscovery, builder.Configuration);
	});

builder.Services.AddTicketingData(azureStorageConnectionString);
builder.Services.AddTicketingDomain();
builder.Services.AddTicketingRest();
builder.Services.AddOpenApi(options =>
{
	options.AddDocumentTransformer((document, _, _) =>
	{
		document.Components ??= new OpenApiComponents();
		document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
		document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
		{
			Type = SecuritySchemeType.Http,
			Scheme = "bearer",
			BearerFormat = "JWT",
			Description = "Microsoft Entra ID access token."
		};

		document.Security ??= [];
		document.Security.Add(new OpenApiSecurityRequirement
		{
			[new OpenApiSecuritySchemeReference("Bearer", document, null)] = []
		});

		return Task.CompletedTask;
	});
});
builder.Services.AddHealthChecks();

var app = builder.Build();

// Configure the HTTP request pipeline.

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapOpenApi();
app.MapScalarApiReference("/api/docs", options =>
{
	options.Title = "Ticketing API";
	options.PersistentAuthentication = true;
});

app.MapTicketingOAuthDiscovery();
app.MapTicketingRestApi();
app.MapHealthChecks("/health", new HealthCheckOptions()).AllowAnonymous();

app.Run();

static void ConfigureRoles(
	TicketingAppRoleOptions options,
	IConfiguration configuration)
{
	options.Technician = GetConfiguredValue(
		configuration,
		"TICKETING_AUTH_ROLE_TECHNICIAN",
		"TicketingAuth:Roles:Technician")
		?? options.Technician;
	options.Manager = GetConfiguredValue(
		configuration,
		"TICKETING_AUTH_ROLE_MANAGER",
		"TicketingAuth:Roles:Manager")
		?? options.Manager;
	options.Admin = GetConfiguredValue(
		configuration,
		"TICKETING_AUTH_ROLE_ADMIN",
		"TicketingAuth:Roles:Admin")
		?? options.Admin;
}

static void ConfigureScopes(
	TicketingScopeOptions options,
	IConfiguration configuration)
{
	options.RequireScopes = GetConfiguredBool(
		configuration,
		options.RequireScopes,
		"TICKETING_AUTH_REQUIRE_SCOPES",
		"TicketingAuth:Scopes:RequireScopes");
	options.Read = GetConfiguredValue(
		configuration,
		"TICKETING_AUTH_SCOPE_READ",
		"TicketingAuth:Scopes:Read")
		?? options.Read;
	options.Write = GetConfiguredValue(
		configuration,
		"TICKETING_AUTH_SCOPE_WRITE",
		"TicketingAuth:Scopes:Write")
		?? options.Write;
	options.Manage = GetConfiguredValue(
		configuration,
		"TICKETING_AUTH_SCOPE_MANAGE",
		"TicketingAuth:Scopes:Manage")
		?? options.Manage;
	options.System = GetConfiguredValue(
		configuration,
		"TICKETING_AUTH_SCOPE_SYSTEM",
		"TicketingAuth:Scopes:System")
		?? options.System;
}

static void ConfigureOAuthDiscovery(
	TicketingOAuthDiscoveryOptions options,
	IConfiguration configuration)
{
	options.Enabled = GetConfiguredBool(
		configuration,
		options.Enabled,
		"TICKETING_AUTH_OAUTH_ENABLED",
		"TicketingAuth:OAuth:Enabled");
	options.AddResourceMetadataToChallenges = GetConfiguredBool(
		configuration,
		options.AddResourceMetadataToChallenges,
		"TICKETING_AUTH_OAUTH_ADD_RESOURCE_METADATA_TO_CHALLENGES",
		"TicketingAuth:OAuth:AddResourceMetadataToChallenges");
	options.ResourceIdentifier = GetConfiguredValue(
		configuration,
		"TICKETING_AUTH_OAUTH_RESOURCE_IDENTIFIER",
		"TicketingAuth:OAuth:ResourceIdentifier",
		"TicketingAuth:OAuth:Resource");
	options.ResourceMetadataUri = GetConfiguredValue(
		configuration,
		"TICKETING_AUTH_OAUTH_RESOURCE_METADATA_URI",
		"TicketingAuth:OAuth:ResourceMetadataUri");
	options.WellKnownPath = GetConfiguredValue(
		configuration,
		"TICKETING_AUTH_OAUTH_WELL_KNOWN_PATH",
		"TicketingAuth:OAuth:WellKnownPath")
		?? options.WellKnownPath;
	options.ResourceApplicationIdUri = GetConfiguredValue(
		configuration,
		"TICKETING_AUTH_OAUTH_RESOURCE_APPLICATION_ID_URI",
		"TicketingAuth:OAuth:ResourceApplicationIdUri",
		"AzureAd:ApplicationIdUri");
	options.ResourceName = GetConfiguredValue(
		configuration,
		"TICKETING_AUTH_OAUTH_RESOURCE_NAME",
		"TicketingAuth:OAuth:ResourceName")
		?? options.ResourceName;
	options.ResourceDocumentationUri = GetConfiguredValue(
		configuration,
		"TICKETING_AUTH_OAUTH_RESOURCE_DOCUMENTATION_URI",
		"TicketingAuth:OAuth:ResourceDocumentationUri");
	options.ResourcePolicyUri = GetConfiguredValue(
		configuration,
		"TICKETING_AUTH_OAUTH_RESOURCE_POLICY_URI",
		"TicketingAuth:OAuth:ResourcePolicyUri");
	options.ResourceTermsOfServiceUri = GetConfiguredValue(
		configuration,
		"TICKETING_AUTH_OAUTH_RESOURCE_TOS_URI",
		"TicketingAuth:OAuth:ResourceTermsOfServiceUri");
	options.AuthorizationServers = GetConfiguredList(
		configuration,
		"TICKETING_AUTH_OAUTH_AUTHORIZATION_SERVERS",
		"TicketingAuth:OAuth:AuthorizationServers");
	options.ScopesSupported = GetConfiguredList(
		configuration,
		"TICKETING_AUTH_OAUTH_SCOPES_SUPPORTED",
		"TicketingAuth:OAuth:ScopesSupported");
	options.ChallengeScopes = GetConfiguredList(
		configuration,
		"TICKETING_AUTH_OAUTH_CHALLENGE_SCOPES",
		"TicketingAuth:OAuth:ChallengeScopes");
	options.BearerMethodsSupported = GetConfiguredList(
		configuration,
		"TICKETING_AUTH_OAUTH_BEARER_METHODS_SUPPORTED",
		"TicketingAuth:OAuth:BearerMethodsSupported")
		?? options.BearerMethodsSupported;
	options.IncludeDefaultScope = GetConfiguredBool(
		configuration,
		options.IncludeDefaultScope,
		"TICKETING_AUTH_OAUTH_INCLUDE_DEFAULT_SCOPE",
		"TicketingAuth:OAuth:IncludeDefaultScope");
	options.IncludeOpenIdScope = GetConfiguredBool(
		configuration,
		options.IncludeOpenIdScope,
		"TICKETING_AUTH_OAUTH_INCLUDE_OPENID_SCOPE",
		"TicketingAuth:OAuth:IncludeOpenIdScope");
	options.IncludeProfileScope = GetConfiguredBool(
		configuration,
		options.IncludeProfileScope,
		"TICKETING_AUTH_OAUTH_INCLUDE_PROFILE_SCOPE",
		"TicketingAuth:OAuth:IncludeProfileScope");
	options.IncludeEmailScope = GetConfiguredBool(
		configuration,
		options.IncludeEmailScope,
		"TICKETING_AUTH_OAUTH_INCLUDE_EMAIL_SCOPE",
		"TicketingAuth:OAuth:IncludeEmailScope");
	options.IncludeOfflineAccessScope = GetConfiguredBool(
		configuration,
		options.IncludeOfflineAccessScope,
		"TICKETING_AUTH_OAUTH_INCLUDE_OFFLINE_ACCESS_SCOPE",
		"TicketingAuth:OAuth:IncludeOfflineAccessScope");
}

static string? GetConfiguredValue(
	IConfiguration configuration,
	params string[] keys)
{
	foreach (var key in keys)
	{
		var value = configuration[key];
		if (!string.IsNullOrWhiteSpace(value))
		{
			return value.Trim();
		}
	}

	return null;
}

static IReadOnlyCollection<string>? GetConfiguredList(
	IConfiguration configuration,
	string environmentKey,
	string configurationKey)
{
	var values = new List<string>();
	var environmentValue = configuration[environmentKey];
	if (!string.IsNullOrWhiteSpace(environmentValue))
	{
		values.AddRange(SplitConfiguredList(environmentValue));
	}

	var configurationValue = configuration[configurationKey];
	if (!string.IsNullOrWhiteSpace(configurationValue))
	{
		values.AddRange(SplitConfiguredList(configurationValue));
	}

	values.AddRange(
		configuration.GetSection(configurationKey)
			.GetChildren()
			.Select(child => child.Value)
			.Where(value => !string.IsNullOrWhiteSpace(value))
			.Select(value => value!.Trim()));

	return values.Count == 0
		? null
		: values.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
}

static bool GetConfiguredBool(
	IConfiguration configuration,
	bool currentValue,
	params string[] keys)
{
	var value = GetConfiguredValue(configuration, keys);
	return bool.TryParse(value, out var parsed)
		? parsed
		: currentValue;
}

static IEnumerable<string> SplitConfiguredList(string value) =>
	value.Split([';', ',', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
