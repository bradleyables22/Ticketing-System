using Azure;
using Azure.Data.Tables;
using Ticketing.Data.AzureStorage.Entities;
using Ticketing.Data.Models;

namespace Ticketing.Data.AzureStorage.Internal;

internal sealed class TicketMutationService
{
	private const int InternalMutationRetryCount = 3;

	private readonly AzureStorageClients _clients;
	private readonly TicketIndexProjector _projector;
	private readonly TicketAuditWriter _auditWriter;

	public TicketMutationService(
		AzureStorageClients clients,
		TicketIndexProjector projector,
		TicketAuditWriter auditWriter)
	{
		_clients = clients;
		_projector = projector;
		_auditWriter = auditWriter;
	}

	public async Task<TicketEntity> MutateAsync(
		string ticketId,
		string? expectedETag,
		Action<TicketEntity> mutate,
		string actorOid,
		TicketAuditEventType eventType,
		string? fieldName,
		string? oldValue,
		string? newValue,
		string? description,
		CancellationToken cancellationToken)
	{
		var attemptCount = string.IsNullOrWhiteSpace(expectedETag) ? InternalMutationRetryCount : 1;

		for (var attempt = 1; attempt <= attemptCount; attempt++)
		{
			var current = await GetRequiredTicketEntityAsync(ticketId, cancellationToken);
			var old = current.Copy();

			mutate(current);

			try
			{
				var ifMatch = string.IsNullOrWhiteSpace(expectedETag) ? old.ETag : new ETag(expectedETag);

				await _clients.Tickets.UpdateEntityAsync(current, ifMatch, TableUpdateMode.Replace, cancellationToken);
				await _projector.ReplaceAsync(old, current, cancellationToken);
				await _auditWriter.AppendAsync(
					ticketId,
					actorOid,
					eventType,
					fieldName,
					oldValue,
					newValue,
					description,
					cancellationToken);

				return current;
			}
			catch (RequestFailedException ex) when (ex.Status == 412 && attempt < attemptCount)
			{
			}
		}

		throw new InvalidOperationException($"Ticket '{ticketId}' could not be updated due to concurrent writes.");
	}

	public async Task<TicketEntity> GetRequiredTicketEntityAsync(string ticketId, CancellationToken cancellationToken)
	{
		var response = await _clients.Tickets.GetEntityIfExistsAsync<TicketEntity>(
			StorageKeys.TicketPartition(ticketId),
			StorageKeys.TicketRow(ticketId),
			cancellationToken: cancellationToken);

		if (!response.HasValue)
		{
			throw new KeyNotFoundException($"Ticket '{ticketId}' was not found.");
		}

		return response.Value!;
	}
}
