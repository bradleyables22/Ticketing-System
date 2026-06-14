using System.Runtime.CompilerServices;
using Azure.Data.Tables;
using Ticketing.Data.AzureStorage.Entities;
using Ticketing.Data.AzureStorage.Internal;
using Ticketing.Data.Models;
using Ticketing.Data.Stores;

namespace Ticketing.Data.AzureStorage;

internal sealed class AzureTicketNoteStore : ITicketNoteStore
{
	private readonly AzureStorageClients _clients;
	private readonly TicketMutationService _mutations;

	public AzureTicketNoteStore(AzureStorageClients clients, TicketMutationService mutations)
	{
		_clients = clients;
		_mutations = mutations;
	}

	public async Task<TicketNoteRecord> AddAsync(AddTicketNoteRequest request, CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(request.TicketId);
		ArgumentException.ThrowIfNullOrWhiteSpace(request.AuthorOid);
		ArgumentException.ThrowIfNullOrWhiteSpace(request.Body);

		await _mutations.GetRequiredTicketEntityAsync(request.TicketId, cancellationToken);

		var createdUtc = DateTimeOffset.UtcNow;
		var noteId = StorageKeys.NewId();
		var entity = new TicketNoteEntity
		{
			PartitionKey = StorageKeys.TicketScopedPartition(request.TicketId),
			RowKey = StorageKeys.NoteRow(createdUtc, noteId),
			NoteId = noteId,
			TicketId = request.TicketId,
			AuthorOid = request.AuthorOid.Trim(),
			IsInternal = request.IsInternal,
			Body = request.Body,
			CreatedUtc = createdUtc
		};

		await _clients.TicketNotes.AddEntityAsync(entity, cancellationToken);
		await _mutations.MutateAsync(
			request.TicketId,
			expectedETag: null,
			ticket =>
			{
				ticket.NoteCount++;
				ticket.LastUpdatedUtc = DateTimeOffset.UtcNow;
			},
			request.AuthorOid,
			TicketAuditEventType.NoteAdded,
			null,
			null,
			noteId,
			request.IsInternal ? "Internal note added." : "Public note added.",
			cancellationToken);

		return entity.ToRecord();
	}

	public async IAsyncEnumerable<TicketNoteRecord> GetForTicketAsync(
		string ticketId,
		bool includeInternal,
		int? pageSize = null,
		[EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(ticketId);

		var partitionKey = StorageKeys.TicketScopedPartition(ticketId);
		var filter = TableClient.CreateQueryFilter($"PartitionKey eq {partitionKey}");
		var normalizedPageSize = AzureTablePageLimits.Normalize(pageSize);
		var returned = 0;

		await foreach (var entity in _clients.TicketNotes
			.QueryAsync<TicketNoteEntity>(filter, maxPerPage: normalizedPageSize, cancellationToken: cancellationToken)
			.ConfigureAwait(false))
		{
			if (!entity.IsInternal || includeInternal)
			{
				yield return entity.ToRecord();
				returned++;
				if (AzureTablePageLimits.IsFull(normalizedPageSize, returned))
				{
					yield break;
				}
			}
		}
	}

	public Task<PagedResult<TicketNoteRecord>> GetForTicketPageAsync(
		string ticketId,
		bool includeInternal,
		int? pageSize = null,
		string? pageToken = null,
		CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(ticketId);

		var partitionKey = StorageKeys.TicketScopedPartition(ticketId);
		var filter = TableClient.CreateQueryFilter($"PartitionKey eq {partitionKey}");
		return AzureTablePagedQueries.QueryPageAsync<TicketNoteEntity, TicketNoteRecord>(
			_clients.TicketNotes,
			filter,
			AzureTablePageLimits.NormalizeResultSize(pageSize),
			pageToken,
			entity => !entity.IsInternal || includeInternal ? entity.ToRecord() : null,
			cancellationToken);
	}
}
