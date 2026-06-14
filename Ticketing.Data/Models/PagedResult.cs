namespace Ticketing.Data.Models;

public sealed record PagedResult<T>
{
	public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();

	public string? NextPageToken { get; init; }

	public static PagedResult<T> Empty { get; } = new();
}
