namespace Ticketing.Domain.Services;

internal static class AsyncEnumerableExtensions
{
	public static async Task<IReadOnlyList<T>> ToReadOnlyListAsync<T>(
		this IAsyncEnumerable<T> source,
		CancellationToken cancellationToken)
	{
		var items = new List<T>();

		await foreach (var item in source.WithCancellation(cancellationToken))
		{
			items.Add(item);
		}

		return items;
	}
}
