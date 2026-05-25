namespace Ticketing.Domain.Models;

public sealed record SaveTicketTypeCommand
{
	public string? TypeId { get; init; }

	public required string Name { get; init; }

	public string? Description { get; init; }

	public int SortOrder { get; init; }

	public bool IsActive { get; init; } = true;
}

public sealed record SaveTicketCategoryCommand
{
	public string? CategoryId { get; init; }

	public required string TypeId { get; init; }

	public required string Name { get; init; }

	public string? Description { get; init; }

	public int SortOrder { get; init; }

	public bool IsActive { get; init; } = true;
}

public sealed record SaveTicketSubcategoryCommand
{
	public string? SubcategoryId { get; init; }

	public required string TypeId { get; init; }

	public required string CategoryId { get; init; }

	public required string Name { get; init; }

	public string? Description { get; init; }

	public int SortOrder { get; init; }

	public bool IsActive { get; init; } = true;
}
