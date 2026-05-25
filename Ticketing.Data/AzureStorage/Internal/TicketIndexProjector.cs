using Azure;
using Azure.Data.Tables;
using Ticketing.Data.AzureStorage.Entities;
using Ticketing.Data.Models;

namespace Ticketing.Data.AzureStorage.Internal;

internal sealed class TicketIndexProjector
{
	private readonly AzureStorageClients _clients;

	public TicketIndexProjector(AzureStorageClients clients)
	{
		_clients = clients;
	}

	public async Task ReplaceAsync(TicketEntity? oldTicket, TicketEntity newTicket, CancellationToken cancellationToken)
	{
		if (oldTicket is not null)
		{
			await DeleteAsync(oldTicket, cancellationToken);
		}

		await UpsertAsync(newTicket, cancellationToken);
	}

	public async Task UpsertAsync(TicketEntity ticket, CancellationToken cancellationToken)
	{
		var status = Enum.Parse<TicketStatus>(ticket.Status);
		var rowKey = StorageKeys.IndexRow(ticket.LastUpdatedUtc, ticket.TicketId);

		await UpsertIndexAsync(
			_clients.TicketsByAssignee,
			StorageKeys.AssigneePartition(ticket.AssigneeOid, status),
			rowKey,
			ticket,
			cancellationToken);

		await UpsertIndexAsync(
			_clients.TicketsBySubmitter,
			StorageKeys.SubmitterPartition(ticket.SubmitterOid, status),
			rowKey,
			ticket,
			cancellationToken);

		await UpsertIndexAsync(
			_clients.TicketsByStatus,
			StorageKeys.StatusPartition(status),
			rowKey,
			ticket,
			cancellationToken);

		await UpsertIndexAsync(
			_clients.TicketsByTeam,
			StorageKeys.TeamQueuePartition(ticket.AssignedTeamId, status),
			rowKey,
			ticket,
			cancellationToken);

		await UpsertIndexAsync(
			_clients.TicketsByQueue,
			StorageKeys.QueuePartition(ticket.TypeId, ticket.CategoryId, ticket.SubcategoryId, status),
			rowKey,
			ticket,
			cancellationToken);

		foreach (var tag in StorageKeys.DeserializeTags(ticket.TagsJson))
		{
			await UpsertIndexAsync(
				_clients.TicketsByTag,
				StorageKeys.TagPartition(tag, status),
				rowKey,
				ticket,
				cancellationToken);
		}
	}

	public async Task DeleteAsync(TicketEntity ticket, CancellationToken cancellationToken)
	{
		var status = Enum.Parse<TicketStatus>(ticket.Status);
		var rowKey = StorageKeys.IndexRow(ticket.LastUpdatedUtc, ticket.TicketId);

		await DeleteIndexAsync(_clients.TicketsByAssignee, StorageKeys.AssigneePartition(ticket.AssigneeOid, status), rowKey, cancellationToken);
		await DeleteIndexAsync(_clients.TicketsBySubmitter, StorageKeys.SubmitterPartition(ticket.SubmitterOid, status), rowKey, cancellationToken);
		await DeleteIndexAsync(_clients.TicketsByStatus, StorageKeys.StatusPartition(status), rowKey, cancellationToken);
		await DeleteIndexAsync(_clients.TicketsByTeam, StorageKeys.TeamQueuePartition(ticket.AssignedTeamId, status), rowKey, cancellationToken);
		await DeleteIndexAsync(_clients.TicketsByQueue, StorageKeys.QueuePartition(ticket.TypeId, ticket.CategoryId, ticket.SubcategoryId, status), rowKey, cancellationToken);

		foreach (var tag in StorageKeys.DeserializeTags(ticket.TagsJson))
		{
			await DeleteIndexAsync(_clients.TicketsByTag, StorageKeys.TagPartition(tag, status), rowKey, cancellationToken);
		}
	}

	private static Task UpsertIndexAsync(
		TableClient table,
		string partitionKey,
		string rowKey,
		TicketEntity ticket,
		CancellationToken cancellationToken)
	{
		var entity = TicketIndexEntity.Create(partitionKey, rowKey, ticket);
		return table.UpsertEntityAsync(entity, TableUpdateMode.Replace, cancellationToken);
	}

	private static async Task DeleteIndexAsync(
		TableClient table,
		string partitionKey,
		string rowKey,
		CancellationToken cancellationToken)
	{
		try
		{
			await table.DeleteEntityAsync(partitionKey, rowKey, ETag.All, cancellationToken);
		}
		catch (RequestFailedException ex) when (ex.Status == 404)
		{
		}
	}
}
