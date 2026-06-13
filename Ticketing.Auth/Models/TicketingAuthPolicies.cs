namespace Ticketing.Auth;

public static class TicketingAuthPolicies
{
	public const string SubmitTicket = "Ticketing.SubmitTicket";

	public const string ViewAllTickets = "Ticketing.ViewAllTickets";

	public const string WorkTicket = "Ticketing.WorkTicket";

	public const string ManageTeams = "Ticketing.ManageTeams";

	public const string ManageTaxonomy = "Ticketing.ManageTaxonomy";

	public const string Admin = "Ticketing.Admin";

	public const string Read = "Ticketing.Scope.Read";

	public const string Write = "Ticketing.Scope.Write";

	public const string Manage = "Ticketing.Scope.Manage";

	public const string System = "Ticketing.Scope.System";
}
