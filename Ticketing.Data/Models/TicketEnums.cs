namespace Ticketing.Data.Models;

public enum TicketStatus
{
	Open = 0,
	InProgress = 1,
	PendingRequester = 2,
	PendingVendor = 3,
	Resolved = 4,
	Closed = 5,
	Cancelled = 6
}

public enum TicketPriority
{
	Low = 1,
	Normal = 2,
	High = 3,
	Critical = 4
}

public enum TicketAuditEventType
{
	TicketCreated = 0,
	DetailsChanged = 1,
	Assigned = 2,
	StatusChanged = 3,
	NoteAdded = 4,
	AttachmentAdded = 5,
	AttachmentDeleted = 6,
	ClassificationChanged = 7,
	TagsChanged = 8,
	Closed = 9,
	Reopened = 10,
	TeamAssigned = 11
}

public enum TeamMemberRole
{
	Member = 0,
	Lead = 1
}
