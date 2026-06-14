using System.Text;
using System.Text.Json;
using Azure;
using Azure.Data.Tables;
using Ticketing.Data.Models;

namespace Ticketing.Data.AzureStorage.Internal;

internal static class AzureTablePagedQueries
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	public static async Task<PagedResult<TResult>> QueryPageAsync<TEntity, TResult>(
		TableClient table,
		string? filter,
		int pageSize,
		string? pageToken,
		Func<TEntity, TResult?> map,
		CancellationToken cancellationToken)
		where TEntity : class, ITableEntity, new()
		where TResult : class
	{
		var continuationToken = DecodeSingleToken(pageToken);
		var items = new List<TResult>(pageSize);

		while (items.Count < pageSize)
		{
			var page = await ReadNextPageAsync<TEntity>(
				table,
				filter,
				pageSize - items.Count,
				continuationToken,
				cancellationToken);

			if (page is null)
			{
				break;
			}

			foreach (var entity in page.Values)
			{
				if (map(entity) is { } item)
				{
					items.Add(item);
				}
			}

			continuationToken = page.ContinuationToken;
			if (continuationToken is null)
			{
				break;
			}
		}

		return new PagedResult<TResult>
		{
			Items = items,
			NextPageToken = EncodeSingleToken(continuationToken)
		};
	}

	public static async Task<PagedResult<TResult>> QuerySegmentedPageAsync<TEntity, TResult>(
		TableClient table,
		IReadOnlyList<string> partitionKeys,
		int pageSize,
		string? pageToken,
		Func<TEntity, TResult> map,
		CancellationToken cancellationToken)
		where TEntity : class, ITableEntity, new()
	{
		if (partitionKeys.Count == 0)
		{
			return PagedResult<TResult>.Empty;
		}

		var token = DecodeSegmentedToken(pageToken);
		var segmentIndex = Math.Clamp(token?.SegmentIndex ?? 0, 0, partitionKeys.Count - 1);
		var continuationToken = token?.ContinuationToken;
		var items = new List<TResult>(pageSize);

		while (segmentIndex < partitionKeys.Count && items.Count < pageSize)
		{
			var filter = TableClient.CreateQueryFilter($"PartitionKey eq {partitionKeys[segmentIndex]}");
			var page = await ReadNextPageAsync<TEntity>(
				table,
				filter,
				pageSize - items.Count,
				continuationToken,
				cancellationToken);

			if (page is null)
			{
				break;
			}

			items.AddRange(page.Values.Select(map));
			continuationToken = page.ContinuationToken;

			if (continuationToken is not null)
			{
				break;
			}

			segmentIndex++;
		}

		string? nextPageToken = null;
		if (continuationToken is not null)
		{
			nextPageToken = EncodeSegmentedToken(new AzureTableSegmentedPageToken(segmentIndex, continuationToken));
		}
		else if (segmentIndex < partitionKeys.Count)
		{
			nextPageToken = EncodeSegmentedToken(new AzureTableSegmentedPageToken(segmentIndex, null));
		}

		return new PagedResult<TResult>
		{
			Items = items,
			NextPageToken = nextPageToken
		};
	}

	private static async Task<Page<TEntity>?> ReadNextPageAsync<TEntity>(
		TableClient table,
		string? filter,
		int pageSize,
		string? continuationToken,
		CancellationToken cancellationToken)
		where TEntity : class, ITableEntity, new()
	{
		if (pageSize < 1)
		{
			return null;
		}

		await foreach (var page in table
			.QueryAsync<TEntity>(filter, maxPerPage: pageSize, cancellationToken: cancellationToken)
			.AsPages(continuationToken, pageSize)
			.ConfigureAwait(false))
		{
			return page;
		}

		return null;
	}

	private static string? DecodeSingleToken(string? pageToken) =>
		string.IsNullOrWhiteSpace(pageToken)
			? null
			: Decode<AzureTableSinglePageToken>(pageToken).ContinuationToken;

	private static string? EncodeSingleToken(string? continuationToken) =>
		string.IsNullOrWhiteSpace(continuationToken)
			? null
			: Encode(new AzureTableSinglePageToken(continuationToken));

	private static AzureTableSegmentedPageToken? DecodeSegmentedToken(string? pageToken) =>
		string.IsNullOrWhiteSpace(pageToken)
			? null
			: Decode<AzureTableSegmentedPageToken>(pageToken);

	private static string EncodeSegmentedToken(AzureTableSegmentedPageToken token) =>
		Encode(token);

	private static T Decode<T>(string pageToken)
	{
		try
		{
			var bytes = Convert.FromBase64String(pageToken);
			return JsonSerializer.Deserialize<T>(bytes, JsonOptions)
				?? throw new ArgumentException("Page token is empty.", nameof(pageToken));
		}
		catch (Exception exception) when (exception is FormatException or JsonException)
		{
			throw new ArgumentException("Page token is invalid.", nameof(pageToken), exception);
		}
	}

	private static string Encode<T>(T value) =>
		Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, JsonOptions)));

	private sealed record AzureTableSinglePageToken(string ContinuationToken);

	private sealed record AzureTableSegmentedPageToken(int SegmentIndex, string? ContinuationToken);
}
