namespace Ticketing.Domain.Configuration;

public sealed class TicketEmailNotificationOptions
{
	public bool Enabled { get; set; } = true;

	public int MaxTeamRecipients { get; set; } = 50;

	public bool ExcludeActorFromRecipients { get; set; } = true;

	public bool IncludeTicketDescription { get; set; }

	public TicketEmailNotificationEventOptions Events { get; set; } = new();
}

public sealed class TicketEmailNotificationEventOptions
{
	public bool TicketCreated { get; set; } = true;

	public bool TicketUpdated { get; set; } = true;

	public bool TicketAssigned { get; set; } = true;

	public bool TeamAssigned { get; set; } = true;

	public bool StatusChanged { get; set; } = true;

	public bool TicketClosed { get; set; } = true;

	public bool TicketReopened { get; set; } = true;

	public bool PublicNoteAdded { get; set; } = true;

	public bool InternalNoteAdded { get; set; } = true;

	public bool AttachmentAdded { get; set; } = true;

	public bool AttachmentDeleted { get; set; } = true;
}
