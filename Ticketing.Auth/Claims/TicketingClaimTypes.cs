namespace Ticketing.Auth.Claims;

public static class TicketingClaimTypes
{
	public const string ObjectId = "oid";

	public const string TenantId = "tid";

	public const string Name = "name";

	public const string PreferredUsername = "preferred_username";

	public const string Upn = "upn";

	public const string Email = "email";

	public const string Roles = "roles";

	public const string Scopes = "scp";

	public const string Scope = "scope";

	public const string LegacyObjectId = "http://schemas.microsoft.com/identity/claims/objectidentifier";
}
