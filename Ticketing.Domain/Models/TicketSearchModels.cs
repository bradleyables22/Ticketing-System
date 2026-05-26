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
}

public sealed record TicketDashboardSummary
{
	public IReadOnlyList<TicketStatusCount> StatusCounts { get; init; } = Array.Empty<TicketStatusCount>();

	public int MyOpenTicketCount { get; init; }

	public int AssignedToMeCount { get; init; }

	public int UnassignedOpenCount { get; init; }

	public int PendingRequesterCount { get; init; }

	public int PendingVendorCount { get; init; }

	public int ResolvedCount { get; init; }

	public IReadOnlyList<TeamTicketCount> TeamCounts { get; init; } = Array.Empty<TeamTicketCount>();
}

public sealed record TicketStatusCount
{
	public TicketStatus Status { get; init; }

	public int Count { get; init; }
}

public sealed record TeamTicketCount
{
	public required string TeamId { get; init; }

	public int OpenCount { get; init; }

	public int InProgressCount { get; init; }

	public int PendingCount { get; init; }
}
