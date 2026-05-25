namespace Ticketing.Data.Models;

public sealed record TeamRecord
{
	public required string TeamId { get; init; }

	public required string Name { get; init; }

	public string? Description { get; init; }

	public bool IsActive { get; init; } = true;

	public required string CreatedByOid { get; init; }

	public DateTimeOffset CreatedUtc { get; init; }

	public string? UpdatedByOid { get; init; }

	public DateTimeOffset? UpdatedUtc { get; init; }

	public string? ETag { get; init; }
}

public sealed record TeamMemberRecord
{
	public required string TeamId { get; init; }

	public required string UserOid { get; init; }

	public TeamMemberRole Role { get; init; }

	public bool IsActive { get; init; } = true;

	public required string CreatedByOid { get; init; }

	public DateTimeOffset CreatedUtc { get; init; }

	public string? UpdatedByOid { get; init; }

	public DateTimeOffset? UpdatedUtc { get; init; }

	public string? ETag { get; init; }
}

public sealed record TeamCategoryAssignmentRecord
{
	public required string AssignmentId { get; init; }

	public required string TeamId { get; init; }

	public string? TypeId { get; init; }

	public string? CategoryId { get; init; }

	public string? SubcategoryId { get; init; }

	public TicketPriority? Priority { get; init; }

	public bool IsDefault { get; init; }

	public bool IsActive { get; init; } = true;

	public int SortOrder { get; init; }

	public required string CreatedByOid { get; init; }

	public DateTimeOffset CreatedUtc { get; init; }

	public string? UpdatedByOid { get; init; }

	public DateTimeOffset? UpdatedUtc { get; init; }

	public string? ETag { get; init; }
}

public sealed record TeamRouteResolution
{
	public required string TeamId { get; init; }

	public required string AssignmentId { get; init; }

	public required string MatchLevel { get; init; }
}
