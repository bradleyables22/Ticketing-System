namespace Ticketing.Data.Models;

public sealed record QueueTicketEmailNotificationRequest
{
	public required string EventName { get; init; }

	public required string TemplateKey { get; init; }

	public required TicketEmailNotificationTicket Ticket { get; init; }

	public required TicketEmailNotificationActor Actor { get; init; }

	public IReadOnlyCollection<TicketEmailNotificationRecipient> Recipients { get; init; } = Array.Empty<TicketEmailNotificationRecipient>();

	public IReadOnlyDictionary<string, string?> Data { get; init; } = new Dictionary<string, string?>();
}

public sealed record TicketEmailNotificationQueueMessage
{
	public int SchemaVersion { get; init; } = 1;

	public required string NotificationId { get; init; }

	public string WorkType { get; init; } = "ticket-email-notification";

	public required string EventName { get; init; }

	public required string TemplateKey { get; init; }

	public required DateTimeOffset CreatedUtc { get; init; }

	public required TicketEmailNotificationTicket Ticket { get; init; }

	public required TicketEmailNotificationActor Actor { get; init; }

	public IReadOnlyCollection<TicketEmailNotificationRecipient> Recipients { get; init; } = Array.Empty<TicketEmailNotificationRecipient>();

	public IReadOnlyDictionary<string, string?> Data { get; init; } = new Dictionary<string, string?>();
}

public sealed record TicketEmailNotificationTicket
{
	public required string TicketId { get; init; }

	public required string TicketNumber { get; init; }

	public required string Title { get; init; }

	public string? Description { get; init; }

	public TicketStatus Status { get; init; }

	public TicketPriority Priority { get; init; }

	public string? TypeId { get; init; }

	public string? CategoryId { get; init; }

	public string? SubcategoryId { get; init; }

	public required string SubmitterOid { get; init; }

	public string? AssigneeOid { get; init; }

	public string? AssignedTeamId { get; init; }

	public DateTimeOffset OpenedUtc { get; init; }

	public DateTimeOffset? ClosedUtc { get; init; }

	public DateTimeOffset LastUpdatedUtc { get; init; }
}

public sealed record TicketEmailNotificationActor
{
	public required string UserOid { get; init; }

	public string? DisplayName { get; init; }

	public string? Email { get; init; }
}

public sealed record TicketEmailNotificationRecipient
{
	public required string UserOid { get; init; }

	public string? DisplayName { get; init; }

	public string? Email { get; init; }

	public IReadOnlyCollection<string> Roles { get; init; } = Array.Empty<string>();
}
