using System.Runtime.CompilerServices;
using Azure.Data.Tables;
using Ticketing.Data.AzureStorage.Entities;
using Ticketing.Data.AzureStorage.Internal;
using Ticketing.Data.Models;
using Ticketing.Data.Stores;

namespace Ticketing.Data.AzureStorage;

internal sealed class AzureTicketAuditStore : ITicketAuditStore
{
	private readonly AzureStorageClients _clients;

	public AzureTicketAuditStore(AzureStorageClients clients)
	{
		_clients = clients;
	}

	public async IAsyncEnumerable<TicketAuditEventRecord> GetForTicketAsync(
		string ticketId,
		int? pageSize = null,
		[EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(ticketId);

		var partitionKey = StorageKeys.TicketScopedPartition(ticketId);
		var filter = TableClient.CreateQueryFilter($"PartitionKey eq {partitionKey}");
		var normalizedPageSize = AzureTablePageLimits.Normalize(pageSize);
		var returned = 0;

		await foreach (var entity in _clients.TicketAudit
			.QueryAsync<TicketAuditEntity>(filter, maxPerPage: normalizedPageSize, cancellationToken: cancellationToken)
			.ConfigureAwait(false))
		{
			yield return entity.ToRecord();
			returned++;
			if (AzureTablePageLimits.IsFull(normalizedPageSize, returned))
			{
				yield break;
			}
		}
	}

	public Task<PagedResult<TicketAuditEventRecord>> GetForTicketPageAsync(
		string ticketId,
		int? pageSize = null,
		string? pageToken = null,
		CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(ticketId);

		var partitionKey = StorageKeys.TicketScopedPartition(ticketId);
		var filter = TableClient.CreateQueryFilter($"PartitionKey eq {partitionKey}");
		return AzureTablePagedQueries.QueryPageAsync<TicketAuditEntity, TicketAuditEventRecord>(
			_clients.TicketAudit,
			filter,
			AzureTablePageLimits.NormalizeResultSize(pageSize),
			pageToken,
			entity => entity.ToRecord(),
			cancellationToken);
	}
}
