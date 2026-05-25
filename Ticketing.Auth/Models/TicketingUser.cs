namespace Ticketing.Auth.Models;

public sealed record TicketingUser
{
	public bool IsAuthenticated { get; init; }

	public string? UserOid { get; init; }

	public string? TenantId { get; init; }

	public string? DisplayName { get; init; }

	public string? Email { get; init; }

	public IReadOnlySet<string> Roles { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

	public IReadOnlySet<string> Scopes { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

	public bool IsInRole(string role) => Roles.Contains(role);

	public string RequireUserOid() =>
		!string.IsNullOrWhiteSpace(UserOid)
			? UserOid
			: throw new InvalidOperationException("The current authenticated principal does not contain an Entra object id claim.");
}
