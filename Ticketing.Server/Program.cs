using Ticketing.Auth.DependencyInjection;
using Ticketing.Data.DependencyInjection;
using Ticketing.Domain.DependencyInjection;

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

var app = builder.Build();

// Configure the HTTP request pipeline.

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();


app.Run();

