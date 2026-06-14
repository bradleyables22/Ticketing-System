namespace Ticketing.Data.Models;

public sealed record TicketRecord
{
	public required string TicketId { get; init; }

	public required string TicketNumber { get; init; }

	public required string Title { get; init; }

	public required string Description { get; init; }

	public TicketStatus Status { get; init; }

	public TicketPriority Priority { get; init; }

	public string? TypeId { get; init; }

	public string? CategoryId { get; init; }

	public string? SubcategoryId { get; init; }

	public required string SubmitterOid { get; init; }

	public required string CreatedByOid { get; init; }

	public string? AssigneeOid { get; init; }

	public string? AssignedTeamId { get; init; }

	public DateTimeOffset OpenedUtc { get; init; }

	public DateTimeOffset? ClosedUtc { get; init; }

	public DateTimeOffset LastUpdatedUtc { get; init; }

	public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();

	public int NoteCount { get; init; }

	public int AttachmentCount { get; init; }

	public string? ETag { get; init; }
}

public sealed record TicketSummary
{
	public required string TicketId { get; init; }

	public required string TicketNumber { get; init; }

	public required string Title { get; init; }

	public TicketStatus Status { get; init; }

	public TicketPriority Priority { get; init; }

	public string? TypeId { get; init; }

	public string? CategoryId { get; init; }

	public string? SubcategoryId { get; init; }

	public required string SubmitterOid { get; init; }

	public required string CreatedByOid { get; init; }

	public string? AssigneeOid { get; init; }

	public string? AssignedTeamId { get; init; }

	public DateTimeOffset OpenedUtc { get; init; }

	public DateTimeOffset? ClosedUtc { get; init; }

	public DateTimeOffset LastUpdatedUtc { get; init; }

	public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();
}

public sealed record TicketNoteRecord
{
	public required string NoteId { get; init; }

	public required string TicketId { get; init; }

	public required string AuthorOid { get; init; }

	public bool IsInternal { get; init; }

	public required string Body { get; init; }

	public DateTimeOffset CreatedUtc { get; init; }

	public DateTimeOffset? EditedUtc { get; init; }

	public string? ETag { get; init; }
}

public sealed record TicketAttachmentRecord
{
	public required string AttachmentId { get; init; }

	public required string TicketId { get; init; }

	public required string BlobContainerName { get; init; }

	public required string BlobName { get; init; }

	public required string OriginalFileName { get; init; }

	public required string ContentType { get; init; }

	public long SizeBytes { get; init; }

	public required string UploadedByOid { get; init; }

	public DateTimeOffset UploadedUtc { get; init; }

	public bool IsDeleted { get; init; }

	public string? ETag { get; init; }
}

public sealed record TicketAuditEventRecord
{
	public required string EventId { get; init; }

	public required string TicketId { get; init; }

	public required string ActorOid { get; init; }

	public TicketAuditEventType EventType { get; init; }

	public string? FieldName { get; init; }

	public string? OldValue { get; init; }

	public string? NewValue { get; init; }

	public string? Description { get; init; }

	public DateTimeOffset CreatedUtc { get; init; }
}

public sealed record TicketUserProfile
{
	public required string UserOid { get; init; }

	public required string DisplayName { get; init; }

	public string? Email { get; init; }

	public string? Department { get; init; }

	public string? JobTitle { get; init; }

	public bool IsActive { get; init; } = true;

	public DateTimeOffset LastSeenUtc { get; init; }

	public string? ETag { get; init; }
}
