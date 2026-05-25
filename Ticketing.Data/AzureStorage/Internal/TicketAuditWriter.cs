using Ticketing.Data.AzureStorage.Entities;
using Ticketing.Data.Models;

namespace Ticketing.Data.AzureStorage.Internal;

internal sealed class TicketAuditWriter
{
	private readonly AzureStorageClients _clients;

	public TicketAuditWriter(AzureStorageClients clients)
	{
		_clients = clients;
	}

	public Task AppendAsync(
		string ticketId,
		string actorOid,
		TicketAuditEventType eventType,
		string? fieldName,
		string? oldValue,
		string? newValue,
		string? description,
		CancellationToken cancellationToken)
	{
		var createdUtc = DateTimeOffset.UtcNow;
		var eventId = StorageKeys.NewId();
		var entity = new TicketAuditEntity
		{
			PartitionKey = StorageKeys.TicketScopedPartition(ticketId),
			RowKey = StorageKeys.AuditRow(createdUtc, eventId),
			EventId = eventId,
			TicketId = ticketId,
			ActorOid = actorOid,
			EventType = eventType.ToString(),
			FieldName = fieldName,
			OldValue = oldValue,
			NewValue = newValue,
			Description = description,
			CreatedUtc = createdUtc
		};

		return _clients.TicketAudit.AddEntityAsync(entity, cancellationToken);
	}
}
