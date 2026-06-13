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

		var returned = 0;
		foreach (var currentStatus in Statuses(status))
		{
			var remaining = AzureTablePageLimits.Remaining(pageSize, returned);
			if (remaining == 0)
			{
				yield break;
			}

			await foreach (var ticket in QueryPartitionAsync(
				_clients.TicketsByAssignee,
				StorageKeys.AssigneePartition(assigneeOid, currentStatus),
				remaining,
				cancellationToken))
			{
				yield return ticket;
				returned++;
				if (AzureTablePageLimits.IsFull(pageSize, returned))
				{
					yield break;
				}
			}
		}
	}

	public async IAsyncEnumerable<TicketSummary> GetUnassignedAsync(
		TicketStatus? status = null,
		int? pageSize = null,
		[EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		var returned = 0;
		foreach (var currentStatus in Statuses(status))
		{
			var remaining = AzureTablePageLimits.Remaining(pageSize, returned);
			if (remaining == 0)
			{
				yield break;
			}

			await foreach (var ticket in QueryPartitionAsync(
				_clients.TicketsByAssignee,
				StorageKeys.AssigneePartition(null, currentStatus),
				remaining,
				cancellationToken))
			{
				yield return ticket;
				returned++;
				if (AzureTablePageLimits.IsFull(pageSize, returned))
				{
					yield break;
				}
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

		var returned = 0;
		foreach (var currentStatus in Statuses(status))
		{
			var remaining = AzureTablePageLimits.Remaining(pageSize, returned);
			if (remaining == 0)
			{
				yield break;
			}

			await foreach (var ticket in QueryPartitionAsync(
				_clients.TicketsBySubmitter,
				StorageKeys.SubmitterPartition(submitterOid, currentStatus),
				remaining,
				cancellationToken))
			{
				yield return ticket;
				returned++;
				if (AzureTablePageLimits.IsFull(pageSize, returned))
				{
					yield break;
				}
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
		var returned = 0;
		foreach (var currentStatus in Statuses(status))
		{
			var remaining = AzureTablePageLimits.Remaining(pageSize, returned);
			if (remaining == 0)
			{
				yield break;
			}

			await foreach (var ticket in QueryPartitionAsync(
				_clients.TicketsByTeam,
				StorageKeys.TeamQueuePartition(teamId, currentStatus),
				remaining,
				cancellationToken))
			{
				yield return ticket;
				returned++;
				if (AzureTablePageLimits.IsFull(pageSize, returned))
				{
					yield break;
				}
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
		var returned = 0;
		foreach (var currentStatus in Statuses(status))
		{
			var remaining = AzureTablePageLimits.Remaining(pageSize, returned);
			if (remaining == 0)
			{
				yield break;
			}

			await foreach (var ticket in QueryPartitionAsync(
				_clients.TicketsByQueue,
				StorageKeys.QueuePartition(typeId, categoryId, subcategoryId, currentStatus),
				remaining,
				cancellationToken))
			{
				yield return ticket;
				returned++;
				if (AzureTablePageLimits.IsFull(pageSize, returned))
				{
					yield break;
				}
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

		var returned = 0;
		foreach (var currentStatus in Statuses(status))
		{
			var remaining = AzureTablePageLimits.Remaining(pageSize, returned);
			if (remaining == 0)
			{
				yield break;
			}

			await foreach (var ticket in QueryPartitionAsync(
				_clients.TicketsByTag,
				StorageKeys.TagPartition(tag, currentStatus),
				remaining,
				cancellationToken))
			{
				yield return ticket;
				returned++;
				if (AzureTablePageLimits.IsFull(pageSize, returned))
				{
					yield break;
				}
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
		var normalizedPageSize = AzureTablePageLimits.Normalize(pageSize);
		var filter = TableClient.CreateQueryFilter($"PartitionKey eq {partitionKey}");
		var returned = 0;

		await foreach (var entity in table
			.QueryAsync<TicketIndexEntity>(filter, maxPerPage: normalizedPageSize, cancellationToken: cancellationToken)
			.ConfigureAwait(false))
		{
			yield return entity.ToSummary();
			returned++;
			if (AzureTablePageLimits.IsFull(normalizedPageSize, returned))
			{
				yield break;
			}
		}
	}
}
