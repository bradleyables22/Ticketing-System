using Ticketing.Data.Models;

namespace Ticketing.Domain.Models;

public sealed record TicketingCurrentUserContext
{
	public required string UserOid { get; init; }

	public string? TenantId { get; init; }

	public required string DisplayName { get; init; }

	public string? Email { get; init; }

	public IReadOnlyCollection<string> Roles { get; init; } = Array.Empty<string>();

	public IReadOnlyCollection<string> Scopes { get; init; } = Array.Empty<string>();

	public TicketingPermissionSet Permissions { get; init; } = new();

	public IReadOnlyList<TeamMemberRecord> TeamMemberships { get; init; } = Array.Empty<TeamMemberRecord>();
}

public sealed record TicketingPermissionSet
{
	public bool CanSubmitTickets { get; init; }

	public bool CanSubmitTicketsOnBehalf { get; init; }

	public bool CanWorkTickets { get; init; }

	public bool CanViewAllTickets { get; init; }

	public bool CanManageTeams { get; init; }

	public bool CanManageTaxonomy { get; init; }

	public bool IsAdmin { get; init; }
}
