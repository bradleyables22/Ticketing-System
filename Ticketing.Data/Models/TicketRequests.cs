namespace Ticketing.Data.Models;

public sealed record CreateTicketRequest
{
	public required string Title { get; init; }

	public required string Description { get; init; }

	public required string SubmitterOid { get; init; }

	public string? AssigneeOid { get; init; }

	public string? AssignedTeamId { get; init; }

	public TicketPriority Priority { get; init; } = TicketPriority.Normal;

	public string? TypeId { get; init; }

	public string? CategoryId { get; init; }

	public string? SubcategoryId { get; init; }

	public IReadOnlyCollection<string> Tags { get; init; } = Array.Empty<string>();
}

public sealed record UpdateTicketDetailsRequest
{
	public required string TicketId { get; init; }

	public required string UpdatedByOid { get; init; }

	public string? ExpectedETag { get; init; }

	public string? Title { get; init; }

	public string? Description { get; init; }

	public TicketPriority? Priority { get; init; }

	public string? TypeId { get; init; }

	public string? CategoryId { get; init; }

	public string? SubcategoryId { get; init; }

	public bool ClearClassification { get; init; }

	public IReadOnlyCollection<string>? Tags { get; init; }
}

public sealed record AssignTicketRequest
{
	public required string TicketId { get; init; }

	public required string ChangedByOid { get; init; }

	public string? AssigneeOid { get; init; }

	public string? Reason { get; init; }

	public string? ExpectedETag { get; init; }
}

public sealed record AssignTicketTeamRequest
{
	public required string TicketId { get; init; }

	public required string ChangedByOid { get; init; }

	public string? AssignedTeamId { get; init; }

	public string? Reason { get; init; }

	public string? ExpectedETag { get; init; }
}

public sealed record SetTicketStatusRequest
{
	public required string TicketId { get; init; }

	public required string ChangedByOid { get; init; }

	public TicketStatus Status { get; init; }

	public string? Reason { get; init; }

	public string? ExpectedETag { get; init; }
}

public sealed record CloseTicketRequest
{
	public required string TicketId { get; init; }

	public required string ClosedByOid { get; init; }

	public string? ResolutionNote { get; init; }

	public string? ExpectedETag { get; init; }
}

public sealed record ReopenTicketRequest
{
	public required string TicketId { get; init; }

	public required string ReopenedByOid { get; init; }

	public string? Reason { get; init; }

	public string? ExpectedETag { get; init; }
}

public sealed record AddTicketNoteRequest
{
	public required string TicketId { get; init; }

	public required string AuthorOid { get; init; }

	public required string Body { get; init; }

	public bool IsInternal { get; init; }
}

public sealed record UploadTicketAttachmentRequest
{
	public required string TicketId { get; init; }

	public required string UploadedByOid { get; init; }

	public required string OriginalFileName { get; init; }

	public string? ContentType { get; init; }

	public required Stream Content { get; init; }

	public long? SizeBytes { get; init; }
}

public sealed record SaveTicketTypeRequest
{
	public string? TypeId { get; init; }

	public required string Name { get; init; }

	public string? Description { get; init; }

	public int SortOrder { get; init; }

	public bool IsActive { get; init; } = true;

	public required string ActorOid { get; init; }
}

public sealed record SaveTicketCategoryRequest
{
	public string? CategoryId { get; init; }

	public required string TypeId { get; init; }

	public required string Name { get; init; }

	public string? Description { get; init; }

	public int SortOrder { get; init; }

	public bool IsActive { get; init; } = true;

	public required string ActorOid { get; init; }
}

public sealed record SaveTicketSubcategoryRequest
{
	public string? SubcategoryId { get; init; }

	public required string TypeId { get; init; }

	public required string CategoryId { get; init; }

	public required string Name { get; init; }

	public string? Description { get; init; }

	public int SortOrder { get; init; }

	public bool IsActive { get; init; } = true;

	public required string ActorOid { get; init; }
}

public sealed record UpsertUserProfileRequest
{
	public required string UserOid { get; init; }

	public required string DisplayName { get; init; }

	public string? Email { get; init; }

	public string? Department { get; init; }

	public string? JobTitle { get; init; }

	public bool IsActive { get; init; } = true;
}
