using System.Runtime.CompilerServices;
using Azure;
using Azure.Data.Tables;
using Azure.Storage.Blobs.Models;
using Ticketing.Data.AzureStorage.Entities;
using Ticketing.Data.AzureStorage.Internal;
using Ticketing.Data.Models;
using Ticketing.Data.Stores;

namespace Ticketing.Data.AzureStorage;

internal sealed class AzureTicketAttachmentStore : ITicketAttachmentStore
{
	private const string DefaultContentType = "application/octet-stream";

	private readonly AzureStorageClients _clients;
	private readonly TicketMutationService _mutations;

	public AzureTicketAttachmentStore(AzureStorageClients clients, TicketMutationService mutations)
	{
		_clients = clients;
		_mutations = mutations;
	}

	public async Task<TicketAttachmentRecord> UploadAsync(UploadTicketAttachmentRequest request, CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(request.TicketId);
		ArgumentException.ThrowIfNullOrWhiteSpace(request.UploadedByOid);
		ArgumentException.ThrowIfNullOrWhiteSpace(request.OriginalFileName);

		await _mutations.GetRequiredTicketEntityAsync(request.TicketId, cancellationToken);

		var uploadedUtc = DateTimeOffset.UtcNow;
		var attachmentId = StorageKeys.NewId();
		var blobName = StorageKeys.AttachmentBlobName(request.TicketId, attachmentId, request.OriginalFileName);
		var contentType = string.IsNullOrWhiteSpace(request.ContentType) ? DefaultContentType : request.ContentType.Trim();
		var blob = _clients.AttachmentsContainer.GetBlobClient(blobName);

		await blob.UploadAsync(
			request.Content,
			new BlobUploadOptions
			{
				HttpHeaders = new BlobHttpHeaders { ContentType = contentType }
			},
			cancellationToken);

		var entity = new TicketAttachmentEntity
		{
			PartitionKey = StorageKeys.TicketScopedPartition(request.TicketId),
			RowKey = StorageKeys.AttachmentRow(uploadedUtc, attachmentId),
			AttachmentId = attachmentId,
			TicketId = request.TicketId,
			BlobContainerName = _clients.AttachmentsContainer.Name,
			BlobName = blobName,
			OriginalFileName = StorageKeys.SafeBlobFileName(request.OriginalFileName),
			ContentType = contentType,
			SizeBytes = request.SizeBytes ?? GetStreamLength(request.Content),
			UploadedByOid = request.UploadedByOid.Trim(),
			UploadedUtc = uploadedUtc
		};

		await _clients.TicketAttachments.AddEntityAsync(entity, cancellationToken);
		await _mutations.MutateAsync(
			request.TicketId,
			expectedETag: null,
			ticket =>
			{
				ticket.AttachmentCount++;
				ticket.LastUpdatedUtc = DateTimeOffset.UtcNow;
			},
			request.UploadedByOid,
			TicketAuditEventType.AttachmentAdded,
			null,
			null,
			attachmentId,
			$"Attachment '{entity.OriginalFileName}' uploaded.",
			cancellationToken);

		return entity.ToRecord();
	}

	public async IAsyncEnumerable<TicketAttachmentRecord> GetForTicketAsync(
		string ticketId,
		bool includeDeleted = false,
		int? pageSize = null,
		[EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(ticketId);

		var partitionKey = StorageKeys.TicketScopedPartition(ticketId);
		var filter = TableClient.CreateQueryFilter($"PartitionKey eq {partitionKey}");

		await foreach (var entity in _clients.TicketAttachments
			.QueryAsync<TicketAttachmentEntity>(filter, maxPerPage: pageSize, cancellationToken: cancellationToken)
			.ConfigureAwait(false))
		{
			if (!entity.IsDeleted || includeDeleted)
			{
				yield return entity.ToRecord();
			}
		}
	}

	public async Task<Stream> OpenReadAsync(string ticketId, string attachmentId, CancellationToken cancellationToken = default)
	{
		var entity = await GetAttachmentEntityAsync(ticketId, attachmentId, cancellationToken);
		if (entity.IsDeleted)
		{
			throw new InvalidOperationException($"Attachment '{attachmentId}' has been deleted.");
		}

		var blob = _clients.AttachmentsContainer.GetBlobClient(entity.BlobName);
		var response = await blob.DownloadStreamingAsync(cancellationToken: cancellationToken);
		return response.Value.Content;
	}

	public async Task SoftDeleteAsync(string ticketId, string attachmentId, string deletedByOid, CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(deletedByOid);

		var entity = await GetAttachmentEntityAsync(ticketId, attachmentId, cancellationToken);
		if (entity.IsDeleted)
		{
			return;
		}

		entity.IsDeleted = true;
		await _clients.TicketAttachments.UpdateEntityAsync(entity, entity.ETag, TableUpdateMode.Replace, cancellationToken);
		await _mutations.MutateAsync(
			ticketId,
			expectedETag: null,
			ticket =>
			{
				ticket.AttachmentCount = Math.Max(0, ticket.AttachmentCount - 1);
				ticket.LastUpdatedUtc = DateTimeOffset.UtcNow;
			},
			deletedByOid,
			TicketAuditEventType.AttachmentDeleted,
			null,
			attachmentId,
			null,
			$"Attachment '{entity.OriginalFileName}' deleted.",
			cancellationToken);
	}

	private async Task<TicketAttachmentEntity> GetAttachmentEntityAsync(
		string ticketId,
		string attachmentId,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(ticketId);
		ArgumentException.ThrowIfNullOrWhiteSpace(attachmentId);

		var partitionKey = StorageKeys.TicketScopedPartition(ticketId);
		var attachmentFilter = TableClient.CreateQueryFilter($"PartitionKey eq {partitionKey} and AttachmentId eq {attachmentId}");

		await foreach (var entity in _clients.TicketAttachments
			.QueryAsync<TicketAttachmentEntity>(attachmentFilter, maxPerPage: 1, cancellationToken: cancellationToken)
			.ConfigureAwait(false))
		{
			return entity;
		}

		throw new KeyNotFoundException($"Attachment '{attachmentId}' was not found for ticket '{ticketId}'.");
	}

	private static long GetStreamLength(Stream content)
	{
		if (!content.CanSeek)
		{
			return 0;
		}

		return Math.Max(0, content.Length - content.Position);
	}
}
