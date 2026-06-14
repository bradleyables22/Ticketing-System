namespace Ticketing.Domain.Services;

using System.Text;
using System.Text.Json;

internal static class DomainPaging
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	public const int DefaultPageSize = 50;

	public const int MaxPageSize = 500;

	public static int NormalizePageSize(int? pageSize) =>
		Math.Clamp(pageSize.GetValueOrDefault(DefaultPageSize), 1, MaxPageSize);

	public static int DecodeOffsetPageToken(string? pageToken)
	{
		if (string.IsNullOrWhiteSpace(pageToken))
		{
			return 0;
		}

		try
		{
			var bytes = Convert.FromBase64String(pageToken);
			var token = JsonSerializer.Deserialize<OffsetPageToken>(bytes, JsonOptions);
			return Math.Max(0, token?.Offset ?? 0);
		}
		catch (Exception exception) when (exception is FormatException or JsonException)
		{
			throw new ArgumentException("Page token is invalid.", nameof(pageToken), exception);
		}
	}

	public static string? EncodeOffsetPageToken(int offset) =>
		offset <= 0
			? null
			: Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new OffsetPageToken(offset), JsonOptions)));

	private sealed record OffsetPageToken(int Offset);
}
