namespace Ticketing.Data.AzureStorage.Internal;

internal static class TicketNumberGenerator
{
	public static string Create(DateTimeOffset openedUtc, string ticketId) =>
		$"T{openedUtc:yyyyMMdd}-{ticketId[..8].ToUpperInvariant()}";
}
