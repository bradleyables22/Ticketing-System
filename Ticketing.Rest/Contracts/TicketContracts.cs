using Ticketing.Data.Models;

namespace Ticketing.Rest.Contracts;

public sealed record CreateTicketHttpRequest
{
	public required string Title { get; init; }

	public required string Description { get; init; }

	public TicketPriority Priority { get; init; } = TicketPriority.Normal;

	public string? TypeId { get; init; }

	public string? CategoryId { get; init; }

	public string? SubcategoryId { get; init; }

	public IReadOnlyCollection<string> Tags { get; init; } = Array.Empty<string>();
}

public sealed record UpdateTicketHttpRequest
{
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

public sealed record AddTicketNoteHttpRequest
{
	public required string Body { get; init; }

	public bool IsInternal { get; init; }
}

public sealed record AssignTicketHttpRequest
{
	public string? AssigneeOid { get; init; }

	public string? Reason { get; init; }

	public string? ExpectedETag { get; init; }
}

public sealed record AssignTicketTeamHttpRequest
{
	public string? AssignedTeamId { get; init; }

	public string? Reason { get; init; }

	public string? ExpectedETag { get; init; }
}

public sealed record SetTicketStatusHttpRequest
{
	public TicketStatus Status { get; init; }

	public string? Reason { get; init; }

	public string? ExpectedETag { get; init; }
}

public sealed record ChangeTicketStatusHttpRequest
{
	public string? Reason { get; init; }

	public string? ExpectedETag { get; init; }
}

public sealed record CloseTicketHttpRequest
{
	public string? ResolutionNote { get; init; }

	public string? ExpectedETag { get; init; }
}

public sealed record ReopenTicketHttpRequest
{
	public string? Reason { get; init; }

	public string? ExpectedETag { get; init; }
}
