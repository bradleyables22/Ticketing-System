using System.Runtime.CompilerServices;
using Azure.Data.Tables;
using Ticketing.Data.AzureStorage.Entities;
using Ticketing.Data.AzureStorage.Internal;
using Ticketing.Data.Models;
using Ticketing.Data.Stores;

namespace Ticketing.Data.AzureStorage;

internal sealed class AzureTicketQueryStore : ITicketQueryStore
{
	private static readonly TicketStatus[] QueryableStatuses =
	[
		TicketStatus.Open,
		TicketStatus.InProgress,
		TicketStatus.PendingRequester,
		TicketStatus.PendingVendor,
		TicketStatus.Resolved,
		TicketStatus.Closed,
		TicketStatus.Cancelled
	];

	private readonly AzureStorageClients _clients;

	public AzureTicketQueryStore(AzureStorageClients clients)
	{
		_clients = clients;
	}

	public async IAsyncEnumerable<TicketSummary> GetAssignedAsync(
		string assigneeOid,
		TicketStatus? status = null,
		int? pageSize = null,
		[EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(assigneeOid);

		foreach (var currentStatus in Statuses(status))
		{
			await foreach (var ticket in QueryPartitionAsync(
				_clients.TicketsByAssignee,
				StorageKeys.AssigneePartition(assigneeOid, currentStatus),
				pageSize,
				cancellationToken))
			{
				yield return ticket;
			}
		}
	}

	public async IAsyncEnumerable<TicketSummary> GetUnassignedAsync(
		TicketStatus? status = null,
		int? pageSize = null,
		[EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		foreach (var currentStatus in Statuses(status))
		{
			await foreach (var ticket in QueryPartitionAsync(
				_clients.TicketsByAssignee,
				StorageKeys.AssigneePartition(null, currentStatus),
				pageSize,
				cancellationToken))
			{
				yield return ticket;
			}
		}
	}

	public async IAsyncEnumerable<TicketSummary> GetSubmittedAsync(
		string submitterOid,
		TicketStatus? status = null,
		int? pageSize = null,
		[EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(submitterOid);

		foreach (var currentStatus in Statuses(status))
		{
			await foreach (var ticket in QueryPartitionAsync(
				_clients.TicketsBySubmitter,
				StorageKeys.SubmitterPartition(submitterOid, currentStatus),
				pageSize,
				cancellationToken))
			{
				yield return ticket;
			}
		}
	}

	public IAsyncEnumerable<TicketSummary> GetByStatusAsync(
		TicketStatus status,
		int? pageSize = null,
		CancellationToken cancellationToken = default) =>
		QueryPartitionAsync(_clients.TicketsByStatus, StorageKeys.StatusPartition(status), pageSize, cancellationToken);

	public async IAsyncEnumerable<TicketSummary> GetByTeamAsync(
		string? teamId,
		TicketStatus? status = null,
		int? pageSize = null,
		[EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		foreach (var currentStatus in Statuses(status))
		{
			await foreach (var ticket in QueryPartitionAsync(
				_clients.TicketsByTeam,
				StorageKeys.TeamQueuePartition(teamId, currentStatus),
				pageSize,
				cancellationToken))
			{
				yield return ticket;
			}
		}
	}

	public async IAsyncEnumerable<TicketSummary> GetByQueueAsync(
		string? typeId,
		string? categoryId,
		string? subcategoryId,
		TicketStatus? status = null,
		int? pageSize = null,
		[EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		foreach (var currentStatus in Statuses(status))
		{
			await foreach (var ticket in QueryPartitionAsync(
				_clients.TicketsByQueue,
				StorageKeys.QueuePartition(typeId, categoryId, subcategoryId, currentStatus),
				pageSize,
				cancellationToken))
			{
				yield return ticket;
			}
		}
	}

	public async IAsyncEnumerable<TicketSummary> GetByTagAsync(
		string tag,
		TicketStatus? status = null,
		int? pageSize = null,
		[EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(tag);

		foreach (var currentStatus in Statuses(status))
		{
			await foreach (var ticket in QueryPartitionAsync(
				_clients.TicketsByTag,
				StorageKeys.TagPartition(tag, currentStatus),
				pageSize,
				cancellationToken))
			{
				yield return ticket;
			}
		}
	}

	private static IEnumerable<TicketStatus> Statuses(TicketStatus? status) =>
		status.HasValue ? [status.Value] : QueryableStatuses;

	private static async IAsyncEnumerable<TicketSummary> QueryPartitionAsync(
		TableClient table,
		string partitionKey,
		int? pageSize,
		[EnumeratorCancellation] CancellationToken cancellationToken)
	{
		var filter = TableClient.CreateQueryFilter($"PartitionKey eq {partitionKey}");

		await foreach (var entity in table
			.QueryAsync<TicketIndexEntity>(filter, maxPerPage: pageSize, cancellationToken: cancellationToken)
			.ConfigureAwait(false))
		{
			yield return entity.ToSummary();
		}
	}
}
