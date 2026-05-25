using Ticketing.Data.Models;

namespace Ticketing.Domain.Models;

public sealed record CreateTicketCommand
{
	public required string Title { get; init; }

	public required string Description { get; init; }

	public TicketPriority Priority { get; init; } = TicketPriority.Normal;

	public string? TypeId { get; init; }

	public string? CategoryId { get; init; }

	public string? SubcategoryId { get; init; }

	public IReadOnlyCollection<string> Tags { get; init; } = Array.Empty<string>();
}

public sealed record UpdateTicketCommand
{
	public required string TicketId { get; init; }

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

public sealed record AddTicketNoteCommand
{
	public required string TicketId { get; init; }

	public required string Body { get; init; }

	public bool IsInternal { get; init; }
}

public sealed record UploadTicketAttachmentCommand
{
	public required string TicketId { get; init; }

	public required string OriginalFileName { get; init; }

	public string? ContentType { get; init; }

	public required Stream Content { get; init; }

	public long? SizeBytes { get; init; }
}

public sealed record AssignTicketCommand
{
	public required string TicketId { get; init; }

	public string? AssigneeOid { get; init; }

	public string? Reason { get; init; }

	public string? ExpectedETag { get; init; }
}

public sealed record AssignTicketTeamCommand
{
	public required string TicketId { get; init; }

	public string? AssignedTeamId { get; init; }

	public string? Reason { get; init; }

	public string? ExpectedETag { get; init; }
}

public sealed record CloseTicketCommand
{
	public required string TicketId { get; init; }

	public string? ResolutionNote { get; init; }

	public string? ExpectedETag { get; init; }
}

public sealed record ReopenTicketCommand
{
	public required string TicketId { get; init; }

	public string? Reason { get; init; }

	public string? ExpectedETag { get; init; }
}
