namespace Ticketing.Data.AzureStorage.Internal;

internal static class AzureTablePageLimits
{
	public static int? Normalize(int? pageSize) =>
		pageSize.HasValue ? Math.Max(1, pageSize.Value) : null;

	public static int? Remaining(int? pageSize, int returned)
	{
		var normalized = Normalize(pageSize);
		if (!normalized.HasValue)
		{
			return null;
		}

		return Math.Max(0, normalized.Value - returned);
	}

	public static bool IsFull(int? pageSize, int returned)
	{
		var normalized = Normalize(pageSize);
		return normalized.HasValue && returned >= normalized.Value;
	}
}
