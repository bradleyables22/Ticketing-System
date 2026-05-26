using Ticketing.Auth.DependencyInjection;
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

app.MapTicketingRestApi();
app.MapHealthChecks("/health", new HealthCheckOptions()).AllowAnonymous();

app.Run();

