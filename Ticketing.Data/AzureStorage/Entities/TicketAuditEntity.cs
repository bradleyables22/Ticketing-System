using Azure;
using Azure.Data.Tables;
using Ticketing.Data.Models;

namespace Ticketing.Data.AzureStorage.Entities;

internal sealed class TicketAuditEntity : ITableEntity
{
	public string PartitionKey { get; set; } = string.Empty;

	public string RowKey { get; set; } = string.Empty;

	public DateTimeOffset? Timestamp { get; set; }

	public ETag ETag { get; set; }

	public string EventId { get; set; } = string.Empty;

	public string TicketId { get; set; } = string.Empty;

	public string ActorOid { get; set; } = string.Empty;

	public string EventType { get; set; } = string.Empty;

	public string? FieldName { get; set; }

	public string? OldValue { get; set; }

	public string? NewValue { get; set; }

	public string? Description { get; set; }

	public DateTimeOffset CreatedUtc { get; set; }

	public TicketAuditEventRecord ToRecord() =>
		new()
		{
			EventId = EventId,
			TicketId = TicketId,
			ActorOid = ActorOid,
			EventType = Enum.Parse<TicketAuditEventType>(EventType),
			FieldName = FieldName,
			OldValue = OldValue,
			NewValue = NewValue,
			Description = Description,
			CreatedUtc = CreatedUtc
		};
}
