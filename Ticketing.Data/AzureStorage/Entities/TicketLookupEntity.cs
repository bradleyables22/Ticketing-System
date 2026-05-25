using Azure;
using Azure.Data.Tables;

namespace Ticketing.Data.AzureStorage.Entities;

internal sealed class TicketLookupEntity : ITableEntity
{
	public string PartitionKey { get; set; } = string.Empty;

	public string RowKey { get; set; } = string.Empty;

	public DateTimeOffset? Timestamp { get; set; }

	public ETag ETag { get; set; }

	public string TicketId { get; set; } = string.Empty;

	public string TicketNumber { get; set; } = string.Empty;

	public string TicketPartitionKey { get; set; } = string.Empty;

	public string TicketRowKey { get; set; } = string.Empty;
}
