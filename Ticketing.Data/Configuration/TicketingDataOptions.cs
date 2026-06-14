namespace Ticketing.Data.Configuration;

public sealed class TicketingDataOptions
{
	public required string ConnectionString { get; set; }

	public TicketingTableNames Tables { get; set; } = new();

	public string AttachmentsContainerName { get; set; } = "ticketattachments";

	public string WorkQueueName { get; set; } = "ticket-work";

	public string EmailNotificationQueueName { get; set; } = "ticket-email-notifications";

	public bool InitializeStorageOnStartup { get; set; } = true;
}

public sealed class TicketingTableNames
{
	public string Tickets { get; set; } = "Tickets";

	public string TicketLookups { get; set; } = "TicketLookups";

	public string TicketNotes { get; set; } = "TicketNotes";

	public string TicketAudit { get; set; } = "TicketAudit";

	public string TicketAttachments { get; set; } = "TicketAttachments";

	public string TicketTaxonomy { get; set; } = "TicketTaxonomy";

	public string UserProfiles { get; set; } = "UserProfiles";

	public string TicketsByAssignee { get; set; } = "TicketsByAssignee";

	public string TicketsBySubmitter { get; set; } = "TicketsBySubmitter";

	public string TicketsByStatus { get; set; } = "TicketsByStatus";

	public string TicketsByQueue { get; set; } = "TicketsByQueue";

	public string TicketsByTag { get; set; } = "TicketsByTag";

	public string TicketsByTeam { get; set; } = "TicketsByTeam";

	public string Teams { get; set; } = "Teams";

	public string TeamMembers { get; set; } = "TeamMembers";

	public string TeamRouting { get; set; } = "TeamRouting";
}
