using Ticketing.Auth.Configuration;
using Ticketing.Auth.DependencyInjection;
using Ticketing.Auth.Endpoints;
using Ticketing.Data.Configuration;
using Ticketing.Data.DependencyInjection;
using Ticketing.Domain.DependencyInjection;
using Ticketing.Rest.DependencyInjection;
using Ticketing.Rest.Endpoints;
using Ticketing.Server.LocalDevelopment;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.OpenApi;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var azureStorageConnectionString =
	builder.Configuration["TICKETING_AZURE_STORAGE_CONNECTION_STRING"]
	?? builder.Configuration.GetConnectionString("AzureStorage")
	?? (builder.Environment.IsDevelopment() ? "UseDevelopmentStorage=true" : null)
	?? throw new InvalidOperationException(
		"Azure Storage connection string is required. Set TICKETING_AZURE_STORAGE_CONNECTION_STRING or ConnectionStrings__AzureStorage.");

var authMode = GetConfiguredValue(builder.Configuration, "TICKETING_AUTH_MODE", "TicketingAuth:Mode")
	?? (builder.Environment.IsDevelopment() ? "Development" : "Entra");
var useDevelopmentAuth = IsDevelopmentAuthMode(authMode);
if (useDevelopmentAuth && !builder.Environment.IsDevelopment())
{
	throw new InvalidOperationException("Ticketing development auth can only be enabled when ASPNETCORE_ENVIRONMENT is Development.");
}

if (useDevelopmentAuth)
{
	builder.Services.AddTicketingDevelopmentAuth(
		options => ConfigureCommonAuthOptions(options, builder.Configuration),
		options => ConfigureDevelopmentAuth(options, builder.Configuration));
}
else
{
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

	builder.Services.AddTicketingAuth(
		azureAdTenantId,
		azureAdClientId,
		options => ConfigureCommonAuthOptions(options, builder.Configuration));
}

if (builder.Environment.IsDevelopment())
{
	builder.Services.Configure<LocalAzuriteOptions>(
		builder.Configuration.GetSection("Ticketing:LocalDevelopment:Azurite"));
	builder.Services.AddHostedService<LocalAzuriteHostedService>();
}

builder.Services.AddTicketingData(azureStorageConnectionString);
builder.Services.AddTicketingGraphUserDirectory(options => ConfigureGraphUserDirectory(options, builder.Configuration));
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

static void ConfigureCommonAuthOptions(
	TicketingAuthOptions options,
	IConfiguration configuration)
{
	options.Instance = configuration["TICKETING_AUTH_INSTANCE"] ?? options.Instance;
	ConfigureRoles(options.Roles, configuration);
	ConfigureScopes(options.Scopes, configuration);
	ConfigureOAuthDiscovery(options.OAuthDiscovery, configuration);
}

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

static void ConfigureDevelopmentAuth(
	TicketingDevelopmentAuthOptions options,
	IConfiguration configuration)
{
	options.UserOid = GetConfiguredValue(
		configuration,
		"TICKETING_AUTH_DEV_USER_OID",
		"TicketingAuth:Development:UserOid")
		?? options.UserOid;
	options.TenantId = GetConfiguredValue(
		configuration,
		"TICKETING_AUTH_DEV_TENANT_ID",
		"TicketingAuth:Development:TenantId")
		?? options.TenantId;
	options.DisplayName = GetConfiguredValue(
		configuration,
		"TICKETING_AUTH_DEV_DISPLAY_NAME",
		"TicketingAuth:Development:DisplayName")
		?? options.DisplayName;
	options.Email = GetConfiguredValue(
		configuration,
		"TICKETING_AUTH_DEV_EMAIL",
		"TicketingAuth:Development:Email")
		?? options.Email;
	options.Roles = GetConfiguredList(
		configuration,
		"TICKETING_AUTH_DEV_ROLES",
		"TicketingAuth:Development:Roles")
		?? options.Roles;
	options.Scopes = GetConfiguredList(
		configuration,
		"TICKETING_AUTH_DEV_SCOPES",
		"TicketingAuth:Development:Scopes")
		?? options.Scopes;
	options.AllowHeaderOverrides = GetConfiguredBool(
		configuration,
		options.AllowHeaderOverrides,
		"TICKETING_AUTH_DEV_ALLOW_HEADER_OVERRIDES",
		"TicketingAuth:Development:AllowHeaderOverrides");
	options.UserOidHeader = GetConfiguredValue(
		configuration,
		"TICKETING_AUTH_DEV_USER_OID_HEADER",
		"TicketingAuth:Development:UserOidHeader")
		?? options.UserOidHeader;
	options.TenantIdHeader = GetConfiguredValue(
		configuration,
		"TICKETING_AUTH_DEV_TENANT_ID_HEADER",
		"TicketingAuth:Development:TenantIdHeader")
		?? options.TenantIdHeader;
	options.DisplayNameHeader = GetConfiguredValue(
		configuration,
		"TICKETING_AUTH_DEV_DISPLAY_NAME_HEADER",
		"TicketingAuth:Development:DisplayNameHeader")
		?? options.DisplayNameHeader;
	options.EmailHeader = GetConfiguredValue(
		configuration,
		"TICKETING_AUTH_DEV_EMAIL_HEADER",
		"TicketingAuth:Development:EmailHeader")
		?? options.EmailHeader;
	options.RolesHeader = GetConfiguredValue(
		configuration,
		"TICKETING_AUTH_DEV_ROLES_HEADER",
		"TicketingAuth:Development:RolesHeader")
		?? options.RolesHeader;
	options.ScopesHeader = GetConfiguredValue(
		configuration,
		"TICKETING_AUTH_DEV_SCOPES_HEADER",
		"TicketingAuth:Development:ScopesHeader")
		?? options.ScopesHeader;
}

static void ConfigureGraphUserDirectory(
	TicketingGraphUserDirectoryOptions options,
	IConfiguration configuration)
{
	options.Enabled = GetConfiguredBool(
		configuration,
		options.Enabled,
		"TICKETING_GRAPH_ENABLED",
		"Ticketing:Graph:Enabled");
	options.TenantId = GetConfiguredValue(
		configuration,
		"TICKETING_GRAPH_TENANT_ID",
		"Ticketing:Graph:TenantId",
		"TICKETING_AUTH_TENANT_ID",
		"AzureAd:TenantId")
		?? options.TenantId;
	options.ClientId = GetConfiguredValue(
		configuration,
		"TICKETING_GRAPH_CLIENT_ID",
		"Ticketing:Graph:ClientId")
		?? options.ClientId;
	options.ClientSecret = GetConfiguredValue(
		configuration,
		"TICKETING_GRAPH_CLIENT_SECRET",
		"Ticketing:Graph:ClientSecret")
		?? options.ClientSecret;
	options.GraphBaseUri = GetConfiguredValue(
		configuration,
		"TICKETING_GRAPH_BASE_URI",
		"Ticketing:Graph:GraphBaseUri")
		?? options.GraphBaseUri;
}

static bool IsDevelopmentAuthMode(string authMode) =>
	authMode.Equals("Development", StringComparison.OrdinalIgnoreCase)
	|| authMode.Equals("Dev", StringComparison.OrdinalIgnoreCase)
	|| authMode.Equals("Local", StringComparison.OrdinalIgnoreCase);

static IEnumerable<string> SplitConfiguredList(string value) =>
	value.Split([';', ',', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
