namespace Ticketing.Auth.Configuration;

public sealed class TicketingAuthOptions
{
	public string Instance { get; set; } = "https://login.microsoftonline.com/";

	public required string TenantId { get; set; }

	public required string ClientId { get; set; }

	public bool RequireHttpsMetadata { get; set; } = true;

	public IReadOnlyCollection<string>? AdditionalValidAudiences { get; set; }

	public TicketingAppRoleOptions Roles { get; set; } = new();

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
