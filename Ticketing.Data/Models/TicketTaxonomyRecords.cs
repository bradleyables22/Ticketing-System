namespace Ticketing.Data.Models;

public sealed record TicketTypeRecord
{
	public required string TypeId { get; init; }

	public required string Name { get; init; }

	public string? Description { get; init; }

	public int SortOrder { get; init; }

	public bool IsActive { get; init; } = true;

	public required string CreatedByOid { get; init; }

	public DateTimeOffset CreatedUtc { get; init; }

	public string? UpdatedByOid { get; init; }

	public DateTimeOffset? UpdatedUtc { get; init; }

	public string? ETag { get; init; }
}

public sealed record TicketCategoryRecord
{
	public required string CategoryId { get; init; }

	public required string TypeId { get; init; }

	public required string Name { get; init; }

	public string? Description { get; init; }

	public int SortOrder { get; init; }

	public bool IsActive { get; init; } = true;

	public required string CreatedByOid { get; init; }

	public DateTimeOffset CreatedUtc { get; init; }

	public string? UpdatedByOid { get; init; }

	public DateTimeOffset? UpdatedUtc { get; init; }

	public string? ETag { get; init; }
}

public sealed record TicketSubcategoryRecord
{
	public required string SubcategoryId { get; init; }

	public required string CategoryId { get; init; }

	public required string TypeId { get; init; }

	public required string Name { get; init; }

	public string? Description { get; init; }

	public int SortOrder { get; init; }

	public bool IsActive { get; init; } = true;

	public required string CreatedByOid { get; init; }

	public DateTimeOffset CreatedUtc { get; init; }

	public string? UpdatedByOid { get; init; }

	public DateTimeOffset? UpdatedUtc { get; init; }

	public string? ETag { get; init; }
}
