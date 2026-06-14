namespace Ticketing.Data.Configuration;

public sealed class TicketingGraphUserDirectoryOptions
{
	public bool Enabled { get; set; }

	public string TenantId { get; set; } = string.Empty;

	public string ClientId { get; set; } = string.Empty;

	public string ClientSecret { get; set; } = string.Empty;

	public string GraphBaseUri { get; set; } = "https://graph.microsoft.com/v1.0";
}
