using Ticketing.Auth;

namespace Ticketing.Mcp.Configuration;

public sealed class TicketingMcpOptions
{
	public string EndpointPath { get; set; } = "/mcp";

	public bool RequireAuthorization { get; set; } = true;

	public string? AuthorizationPolicy { get; set; } = TicketingAuthPolicies.Read;
}
