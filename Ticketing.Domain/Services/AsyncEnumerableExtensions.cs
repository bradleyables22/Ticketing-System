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

	public static async Task<IReadOnlyList<T>> ToReadOnlyListAsync<T>(
		this IAsyncEnumerable<T> source,
		int maxItems,
		CancellationToken cancellationToken)
	{
		if (maxItems < 1)
		{
			throw new ArgumentOutOfRangeException(nameof(maxItems), "Page size must be greater than zero.");
		}

		var items = new List<T>(maxItems);

		await foreach (var item in source.WithCancellation(cancellationToken))
		{
			items.Add(item);
			if (items.Count >= maxItems)
			{
				break;
			}
		}

		return items;
	}
}
