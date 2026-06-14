using Ticketing.Data.Models;

namespace Ticketing.Domain.Models;

public sealed record TicketSearchCriteria
{
	public string? Query { get; init; }

	public TicketStatus? Status { get; init; }

	public TicketPriority? Priority { get; init; }

	public string? SubmitterOid { get; init; }

	public string? AssigneeOid { get; init; }

	public string? AssignedTeamId { get; init; }

	public string? TypeId { get; init; }

	public string? CategoryId { get; init; }

	public string? SubcategoryId { get; init; }

	public string? Tag { get; init; }

	public DateTimeOffset? OpenedFromUtc { get; init; }

	public DateTimeOffset? OpenedToUtc { get; init; }

	public DateTimeOffset? ClosedFromUtc { get; init; }

	public DateTimeOffset? ClosedToUtc { get; init; }

	public int? PageSize { get; init; }

	public string? PageToken { get; init; }
}
