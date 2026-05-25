using System.Security.Claims;

namespace Ticketing.Auth.Claims;

public static class ClaimsPrincipalExtensions
{
	public static string? GetTicketingUserOid(this ClaimsPrincipal principal) =>
		principal.FindFirstValue(TicketingClaimTypes.ObjectId)
		?? principal.FindFirstValue(TicketingClaimTypes.LegacyObjectId);

	public static string? GetTicketingTenantId(this ClaimsPrincipal principal) =>
		principal.FindFirstValue(TicketingClaimTypes.TenantId);

	public static string? GetTicketingDisplayName(this ClaimsPrincipal principal) =>
		principal.FindFirstValue(TicketingClaimTypes.Name)
		?? principal.Identity?.Name;

	public static string? GetTicketingEmail(this ClaimsPrincipal principal) =>
		principal.FindFirstValue(TicketingClaimTypes.Email)
		?? principal.FindFirstValue(TicketingClaimTypes.PreferredUsername)
		?? principal.FindFirstValue(TicketingClaimTypes.Upn);

	public static IReadOnlySet<string> GetTicketingRoles(this ClaimsPrincipal principal) =>
		principal.Claims
			.Where(claim => claim.Type is TicketingClaimTypes.Roles or ClaimTypes.Role)
			.Select(claim => claim.Value)
			.Where(value => !string.IsNullOrWhiteSpace(value))
			.ToHashSet(StringComparer.OrdinalIgnoreCase);

	public static IReadOnlySet<string> GetTicketingScopes(this ClaimsPrincipal principal) =>
		principal.FindAll(TicketingClaimTypes.Scopes)
			.SelectMany(claim => claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
			.ToHashSet(StringComparer.OrdinalIgnoreCase);
}
