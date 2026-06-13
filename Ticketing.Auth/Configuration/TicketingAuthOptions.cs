namespace Ticketing.Auth.Configuration;

public sealed class TicketingAuthOptions
{
	public string Instance { get; set; } = "https://login.microsoftonline.com/";

	public required string TenantId { get; set; }

	public required string ClientId { get; set; }

	public bool RequireHttpsMetadata { get; set; } = true;

	public IReadOnlyCollection<string>? AdditionalValidAudiences { get; set; }

	public TicketingAppRoleOptions Roles { get; set; } = new();

	public TicketingScopeOptions Scopes { get; set; } = new();

	public TicketingOAuthDiscoveryOptions OAuthDiscovery { get; set; } = new();

	public string Authority => $"{Instance.TrimEnd('/')}/{TenantId}/v2.0";

	internal IReadOnlyCollection<string> ValidAudiences
	{
		get
		{
			var audiences = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
			{
				ClientId,
				$"api://{ClientId}"
			};

			if (!string.IsNullOrWhiteSpace(OAuthDiscovery.ResourceApplicationIdUri))
			{
				audiences.Add(OAuthDiscovery.ResourceApplicationIdUri.Trim().TrimEnd('/'));
			}

			if (AdditionalValidAudiences is not null)
			{
				foreach (var audience in AdditionalValidAudiences)
				{
					if (!string.IsNullOrWhiteSpace(audience))
					{
						audiences.Add(audience.Trim());
					}
				}
			}

			return audiences;
		}
	}
}

public sealed class TicketingAppRoleOptions
{
	public string Technician { get; set; } = TicketingAppRoles.Technician;

	public string Manager { get; set; } = TicketingAppRoles.Manager;

	public string Admin { get; set; } = TicketingAppRoles.Admin;
}

public sealed class TicketingScopeOptions
{
	public bool RequireScopes { get; set; } = true;

	public string Read { get; set; } = TicketingAuthScopes.Read;

	public string Write { get; set; } = TicketingAuthScopes.Write;

	public string Manage { get; set; } = TicketingAuthScopes.Manage;

	public string System { get; set; } = TicketingAuthScopes.System;

	internal IReadOnlyCollection<string> All =>
	[
		Read,
		Write,
		Manage,
		System
	];
}

public sealed class TicketingOAuthDiscoveryOptions
{
	public bool Enabled { get; set; } = true;

	public bool AddResourceMetadataToChallenges { get; set; } = true;

	public string WellKnownPath { get; set; } = "/.well-known/oauth-protected-resource";

	public string? ResourceIdentifier { get; set; }

	public string? ResourceMetadataUri { get; set; }

	public string? ResourceApplicationIdUri { get; set; }

	public string ResourceName { get; set; } = "Ticketing API";

	public string? ResourceDocumentationUri { get; set; }

	public string? ResourcePolicyUri { get; set; }

	public string? ResourceTermsOfServiceUri { get; set; }

	public IReadOnlyCollection<string>? AuthorizationServers { get; set; }

	public IReadOnlyCollection<string>? ScopesSupported { get; set; }

	public IReadOnlyCollection<string>? ChallengeScopes { get; set; }

	public IReadOnlyCollection<string> BearerMethodsSupported { get; set; } = ["header"];

	public bool IncludeDefaultScope { get; set; } = true;

	public bool IncludeOpenIdScope { get; set; } = true;

	public bool IncludeProfileScope { get; set; } = true;

	public bool IncludeEmailScope { get; set; } = true;

	public bool IncludeOfflineAccessScope { get; set; } = true;
}
