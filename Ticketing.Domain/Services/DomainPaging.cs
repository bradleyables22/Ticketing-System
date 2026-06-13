namespace Ticketing.Domain.Services;

internal static class DomainPaging
{
	public const int DefaultPageSize = 50;
	public const int MaxPageSize = 500;

	public static int NormalizePageSize(int? pageSize) =>
		Math.Clamp(pageSize.GetValueOrDefault(DefaultPageSize), 1, MaxPageSize);
}
