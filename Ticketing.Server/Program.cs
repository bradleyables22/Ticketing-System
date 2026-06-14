using Ticketing.Auth.Configuration;
using Ticketing.Auth.DependencyInjection;
using Ticketing.Auth.Endpoints;
using Ticketing.Data.Configuration;
using Ticketing.Data.DependencyInjection;
using Ticketing.Domain.Configuration;
using Ticketing.Domain.DependencyInjection;
using Ticketing.Rest.DependencyInjection;
using Ticketing.Rest.Endpoints;
using Ticketing.Server.LocalDevelopment;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http.Features;
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

var attachmentUploadOptions = ConfigureAttachmentUploadOptions(builder.Configuration);
builder.Services.AddSingleton(attachmentUploadOptions);
ConfigureServerUploadLimits(builder, attachmentUploadOptions);

var emailNotificationOptions = ConfigureEmailNotificationOptions(builder.Configuration);
builder.Services.AddSingleton(emailNotificationOptions);

builder.Services.AddTicketingData(
	azureStorageConnectionString,
	options => ConfigureDataOptions(options, builder.Configuration));
builder.Services.AddTicketingGraphUserDirectory(options => ConfigureGraphUserDirectory(options, builder.Configuration));
builder.Services.AddTicketingDomain();
builder.Services.AddTicketingRest();
builder.Services.AddOpenApi(options =>
{
	options.AddDocumentTransformer((document, _, _) =>
	{
		document.Info.Title = "Ticketing API";
		document.Info.Description = "REST API for ticket submission, ticket workflow, team routing, taxonomy setup, user lookup, attachments, audit history, OAuth discovery, and administrative system information.";

		document.Tags ??= new HashSet<OpenApiTag>();
		AddTagDescription(document, "Tickets", "Ticket creation, search, queues, notes, image attachments, audit, assignment, and workflow transitions.");
		AddTagDescription(document, "Teams", "Team definitions, team memberships, and taxonomy routing assignments used to route incoming tickets.");
		AddTagDescription(document, "Taxonomy", "Ticket type, category, and subcategory setup used for classification, routing, search, and reporting.");
		AddTagDescription(document, "Users", "Current-user context and user profile lookup/search for assignment and administration workflows.");
		AddTagDescription(document, "System", "Administrative system metadata and health-oriented host information.");
		AddTagDescription(document, "OAuth", "Protected resource metadata used by OAuth-capable clients to discover authorization requirements.");

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

	options.AddOperationTransformer((operation, _, _) =>
	{
		foreach (var parameter in operation.Parameters ?? [])
		{
			if (GetOpenApiParameterDescription(parameter.Name) is { } description)
			{
				parameter.Description = description;
			}
		}

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

static void AddTagDescription(OpenApiDocument document, string name, string description)
{
	if (document.Tags is not { } tags)
	{
		return;
	}

	var existing = tags.FirstOrDefault(tag => tag.Name?.Equals(name, StringComparison.OrdinalIgnoreCase) == true);
	if (existing is not null)
	{
		existing.Description = description;
		return;
	}

	tags.Add(new OpenApiTag
	{
		Name = name,
		Description = description
	});
}

static string? GetOpenApiParameterDescription(string? name) =>
	name switch
	{
		"ticketId" => "Opaque ticket id returned by create/search/list operations.",
		"ticketNumber" => "Human-readable ticket number, such as TCK-000001.",
		"attachmentId" => "Opaque attachment id returned by the upload or attachment-list endpoint.",
		"noteId" => "Opaque note id returned by the add-note or note-list endpoint.",
		"teamId" => "Opaque team id used by team definitions, memberships, queues, and routing assignments.",
		"userOid" => "Microsoft Entra object id for a user.",
		"assigneeOid" => "Microsoft Entra object id for the assigned technician. Use null in request bodies to clear assignment.",
		"submitterOid" => "Microsoft Entra object id for the ticket requester/submitter. Normal users can only submit as themselves; technicians, managers, and admins can create on behalf of another user.",
		"assignedTeamId" => "Team id for the team currently owning or filtering ticket work.",
		"typeId" => "Ticket type id from the taxonomy endpoints.",
		"categoryId" => "Ticket category id from the taxonomy endpoints.",
		"subcategoryId" => "Ticket subcategory id from the taxonomy endpoints.",
		"assignmentId" => "Opaque team routing assignment id.",
		"status" => "Ticket status filter or target workflow status.",
		"priority" => "Ticket priority filter or priority-specific routing value.",
		"tag" => "Normalized ticket tag value.",
		"q" => "Free-text ticket search query.",
		"query" => "User search query. Graph-backed search checks display name, mail, and userPrincipalName.",
		"includeInactive" => "When true, includes inactive taxonomy, team, membership, routing, or user records where the endpoint supports them.",
		"pageSize" => "Requested page size. Missing values default to 50 and values above 500 are clamped.",
		"pageToken" => "Opaque continuation token from a previous response's nextPageToken.",
		"openedFromUtc" => "Inclusive lower bound for ticket opened timestamp, in UTC.",
		"openedToUtc" => "Inclusive upper bound for ticket opened timestamp, in UTC.",
		"closedFromUtc" => "Inclusive lower bound for ticket closed timestamp, in UTC.",
		"closedToUtc" => "Inclusive upper bound for ticket closed timestamp, in UTC.",
		"If-Match" => "Optional ETag concurrency header. Request-body ExpectedETag takes precedence when both are supplied.",
		"ifMatch" => "Optional ETag concurrency value from the If-Match header. Request-body ExpectedETag takes precedence when both are supplied.",
		"resourcePath" => "Optional protected resource path segment used by path-aware OAuth metadata discovery.",
		_ => null
	};

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

static void ConfigureDataOptions(
	TicketingDataOptions options,
	IConfiguration configuration)
{
	options.AttachmentsContainerName = GetConfiguredValue(
		configuration,
		"TICKETING_ATTACHMENTS_CONTAINER_NAME",
		"Ticketing:Data:AttachmentsContainerName")
		?? options.AttachmentsContainerName;

	options.WorkQueueName = GetConfiguredValue(
		configuration,
		"TICKETING_WORK_QUEUE_NAME",
		"Ticketing:Data:WorkQueueName")
		?? options.WorkQueueName;

	options.EmailNotificationQueueName = GetConfiguredValue(
		configuration,
		"TICKETING_EMAIL_NOTIFICATIONS_QUEUE_NAME",
		"Ticketing:EmailNotifications:QueueName",
		"TICKETING_DATA_EMAIL_NOTIFICATION_QUEUE_NAME",
		"Ticketing:Data:EmailNotificationQueueName")
		?? options.EmailNotificationQueueName;

	options.InitializeStorageOnStartup = GetConfiguredBool(
		configuration,
		options.InitializeStorageOnStartup,
		"TICKETING_INITIALIZE_STORAGE_ON_STARTUP",
		"Ticketing:Data:InitializeStorageOnStartup");
}

static TicketAttachmentUploadOptions ConfigureAttachmentUploadOptions(IConfiguration configuration)
{
	var options = new TicketAttachmentUploadOptions();

	var maxSizeMegabytes = GetConfiguredLong(
		configuration,
		"TICKETING_ATTACHMENTS_MAX_SIZE_MB",
		"Ticketing:Attachments:MaxSizeMegabytes");
	if (maxSizeMegabytes is > 0)
	{
		options.MaxSizeBytes = checked(maxSizeMegabytes.Value * 1024 * 1024);
	}

	var maxSizeBytes = GetConfiguredLong(
		configuration,
		"TICKETING_ATTACHMENTS_MAX_SIZE_BYTES",
		"Ticketing:Attachments:MaxSizeBytes");
	if (maxSizeBytes is > 0)
	{
		options.MaxSizeBytes = maxSizeBytes.Value;
	}

	if (options.MaxSizeBytes <= 0)
	{
		throw new InvalidOperationException("Attachment max size must be greater than zero.");
	}

	options.AllowedContentTypes = GetConfiguredList(
		configuration,
		"TICKETING_ATTACHMENTS_ALLOWED_CONTENT_TYPES",
		"Ticketing:Attachments:AllowedContentTypes")
		?? options.AllowedContentTypes;

	options.AllowedExtensions = GetConfiguredList(
		configuration,
		"TICKETING_ATTACHMENTS_ALLOWED_EXTENSIONS",
		"Ticketing:Attachments:AllowedExtensions")
		?? options.AllowedExtensions;

	options.ValidateImageSignatures = GetConfiguredBool(
		configuration,
		options.ValidateImageSignatures,
		"TICKETING_ATTACHMENTS_VALIDATE_IMAGE_SIGNATURES",
		"Ticketing:Attachments:ValidateImageSignatures");

	return options;
}

static TicketEmailNotificationOptions ConfigureEmailNotificationOptions(IConfiguration configuration)
{
	var options = new TicketEmailNotificationOptions();

	options.Enabled = GetConfiguredBool(
		configuration,
		options.Enabled,
		"TICKETING_EMAIL_NOTIFICATIONS_ENABLED",
		"Ticketing:EmailNotifications:Enabled");
	options.ExcludeActorFromRecipients = GetConfiguredBool(
		configuration,
		options.ExcludeActorFromRecipients,
		"TICKETING_EMAIL_NOTIFICATIONS_EXCLUDE_ACTOR",
		"Ticketing:EmailNotifications:ExcludeActorFromRecipients");
	options.IncludeTicketDescription = GetConfiguredBool(
		configuration,
		options.IncludeTicketDescription,
		"TICKETING_EMAIL_NOTIFICATIONS_INCLUDE_TICKET_DESCRIPTION",
		"Ticketing:EmailNotifications:IncludeTicketDescription");

	var maxTeamRecipients = GetConfiguredLong(
		configuration,
		"TICKETING_EMAIL_NOTIFICATIONS_MAX_TEAM_RECIPIENTS",
		"Ticketing:EmailNotifications:MaxTeamRecipients");
	if (maxTeamRecipients.HasValue)
	{
		options.MaxTeamRecipients = (int)Math.Clamp(maxTeamRecipients.Value, 0, 500);
	}

	ConfigureEmailNotificationEvents(options.Events, configuration);
	return options;
}

static void ConfigureEmailNotificationEvents(
	TicketEmailNotificationEventOptions options,
	IConfiguration configuration)
{
	options.TicketCreated = GetConfiguredBool(
		configuration,
		options.TicketCreated,
		"TICKETING_EMAIL_NOTIFICATIONS_EVENT_TICKET_CREATED",
		"Ticketing:EmailNotifications:Events:TicketCreated");
	options.TicketUpdated = GetConfiguredBool(
		configuration,
		options.TicketUpdated,
		"TICKETING_EMAIL_NOTIFICATIONS_EVENT_TICKET_UPDATED",
		"Ticketing:EmailNotifications:Events:TicketUpdated");
	options.TicketAssigned = GetConfiguredBool(
		configuration,
		options.TicketAssigned,
		"TICKETING_EMAIL_NOTIFICATIONS_EVENT_TICKET_ASSIGNED",
		"Ticketing:EmailNotifications:Events:TicketAssigned");
	options.TeamAssigned = GetConfiguredBool(
		configuration,
		options.TeamAssigned,
		"TICKETING_EMAIL_NOTIFICATIONS_EVENT_TEAM_ASSIGNED",
		"Ticketing:EmailNotifications:Events:TeamAssigned");
	options.StatusChanged = GetConfiguredBool(
		configuration,
		options.StatusChanged,
		"TICKETING_EMAIL_NOTIFICATIONS_EVENT_STATUS_CHANGED",
		"Ticketing:EmailNotifications:Events:StatusChanged");
	options.TicketClosed = GetConfiguredBool(
		configuration,
		options.TicketClosed,
		"TICKETING_EMAIL_NOTIFICATIONS_EVENT_TICKET_CLOSED",
		"Ticketing:EmailNotifications:Events:TicketClosed");
	options.TicketReopened = GetConfiguredBool(
		configuration,
		options.TicketReopened,
		"TICKETING_EMAIL_NOTIFICATIONS_EVENT_TICKET_REOPENED",
		"Ticketing:EmailNotifications:Events:TicketReopened");
	options.PublicNoteAdded = GetConfiguredBool(
		configuration,
		options.PublicNoteAdded,
		"TICKETING_EMAIL_NOTIFICATIONS_EVENT_PUBLIC_NOTE_ADDED",
		"Ticketing:EmailNotifications:Events:PublicNoteAdded");
	options.InternalNoteAdded = GetConfiguredBool(
		configuration,
		options.InternalNoteAdded,
		"TICKETING_EMAIL_NOTIFICATIONS_EVENT_INTERNAL_NOTE_ADDED",
		"Ticketing:EmailNotifications:Events:InternalNoteAdded");
	options.AttachmentAdded = GetConfiguredBool(
		configuration,
		options.AttachmentAdded,
		"TICKETING_EMAIL_NOTIFICATIONS_EVENT_ATTACHMENT_ADDED",
		"Ticketing:EmailNotifications:Events:AttachmentAdded");
	options.AttachmentDeleted = GetConfiguredBool(
		configuration,
		options.AttachmentDeleted,
		"TICKETING_EMAIL_NOTIFICATIONS_EVENT_ATTACHMENT_DELETED",
		"Ticketing:EmailNotifications:Events:AttachmentDeleted");
}

static void ConfigureServerUploadLimits(
	WebApplicationBuilder builder,
	TicketAttachmentUploadOptions attachmentUploadOptions)
{
	const long multipartOverheadBytes = 1024 * 1024;
	var requestBodyLimit = attachmentUploadOptions.MaxSizeBytes >= long.MaxValue - multipartOverheadBytes
		? long.MaxValue
		: attachmentUploadOptions.MaxSizeBytes + multipartOverheadBytes;

	builder.Services.Configure<FormOptions>(options =>
	{
		options.MultipartBodyLengthLimit = requestBodyLimit;
	});

	builder.WebHost.ConfigureKestrel(options =>
	{
		if (!options.Limits.MaxRequestBodySize.HasValue
			|| options.Limits.MaxRequestBodySize.Value < requestBodyLimit)
		{
			options.Limits.MaxRequestBodySize = requestBodyLimit;
		}
	});
}

static bool IsDevelopmentAuthMode(string authMode) =>
	authMode.Equals("Development", StringComparison.OrdinalIgnoreCase)
	|| authMode.Equals("Dev", StringComparison.OrdinalIgnoreCase)
	|| authMode.Equals("Local", StringComparison.OrdinalIgnoreCase);

static long? GetConfiguredLong(
	IConfiguration configuration,
	params string[] keys)
{
	var value = GetConfiguredValue(configuration, keys);
	return long.TryParse(value, out var parsed)
		? parsed
		: null;
}

static IEnumerable<string> SplitConfiguredList(string value) =>
	value.Split([';', ',', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
