using Ticketing.Data.Models;

namespace Ticketing.Rest.Contracts;

public sealed record SaveTeamHttpRequest
{
	public string? TeamId { get; init; }

	public required string Name { get; init; }

	public string? Description { get; init; }

	public bool IsActive { get; init; } = true;
}

public sealed record SaveTeamMemberHttpRequest
{
	public TeamMemberRole Role { get; init; } = TeamMemberRole.Member;

	public bool IsActive { get; init; } = true;
}

public sealed record SaveTeamCategoryAssignmentHttpRequest
{
	public string? AssignmentId { get; init; }

	public string? TypeId { get; init; }

	public string? CategoryId { get; init; }

	public string? SubcategoryId { get; init; }

	public TicketPriority? Priority { get; init; }

	public bool IsDefault { get; init; }

	public bool IsActive { get; init; } = true;

	public int SortOrder { get; init; }
}
