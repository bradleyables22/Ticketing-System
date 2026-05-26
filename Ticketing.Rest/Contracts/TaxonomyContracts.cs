namespace Ticketing.Rest.Contracts;

public sealed record SaveTicketTypeHttpRequest
{
	public string? TypeId { get; init; }

	public required string Name { get; init; }

	public string? Description { get; init; }

	public int SortOrder { get; init; }

	public bool IsActive { get; init; } = true;
}

public sealed record SaveTicketCategoryHttpRequest
{
	public string? CategoryId { get; init; }

	public required string Name { get; init; }

	public string? Description { get; init; }

	public int SortOrder { get; init; }

	public bool IsActive { get; init; } = true;
}

public sealed record SaveTicketSubcategoryHttpRequest
{
	public string? SubcategoryId { get; init; }

	public required string Name { get; init; }

	public string? Description { get; init; }

	public int SortOrder { get; init; }

	public bool IsActive { get; init; } = true;
}
