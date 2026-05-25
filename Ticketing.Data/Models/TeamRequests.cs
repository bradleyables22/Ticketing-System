namespace Ticketing.Data.Models;

public sealed record SaveTeamRequest
{
	public string? TeamId { get; init; }

	public required string Name { get; init; }

	public string? Description { get; init; }

	public bool IsActive { get; init; } = true;

	public required string ActorOid { get; init; }
}

public sealed record SaveTeamMemberRequest
{
	public required string TeamId { get; init; }

	public required string UserOid { get; init; }

	public TeamMemberRole Role { get; init; } = TeamMemberRole.Member;

	public bool IsActive { get; init; } = true;

	public required string ActorOid { get; init; }
}

public sealed record SaveTeamCategoryAssignmentRequest
{
	public string? AssignmentId { get; init; }

	public required string TeamId { get; init; }

	public string? TypeId { get; init; }

	public string? CategoryId { get; init; }

	public string? SubcategoryId { get; init; }

	public TicketPriority? Priority { get; init; }

	public bool IsDefault { get; init; }

	public bool IsActive { get; init; } = true;

	public int SortOrder { get; init; }

	public required string ActorOid { get; init; }
}
