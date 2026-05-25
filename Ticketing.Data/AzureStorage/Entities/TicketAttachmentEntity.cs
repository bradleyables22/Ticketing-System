using Azure;
using Azure.Data.Tables;
using Ticketing.Data.Models;

namespace Ticketing.Data.AzureStorage.Entities;

internal sealed class TicketAttachmentEntity : ITableEntity
{
	public string PartitionKey { get; set; } = string.Empty;

	public string RowKey { get; set; } = string.Empty;

	public DateTimeOffset? Timestamp { get; set; }

	public ETag ETag { get; set; }

	public string AttachmentId { get; set; } = string.Empty;

	public string TicketId { get; set; } = string.Empty;

	public string BlobContainerName { get; set; } = string.Empty;

	public string BlobName { get; set; } = string.Empty;

	public string OriginalFileName { get; set; } = string.Empty;

	public string ContentType { get; set; } = "application/octet-stream";

	public long SizeBytes { get; set; }

	public string UploadedByOid { get; set; } = string.Empty;

	public DateTimeOffset UploadedUtc { get; set; }

	public bool IsDeleted { get; set; }

	public TicketAttachmentRecord ToRecord() =>
		new()
		{
			AttachmentId = AttachmentId,
			TicketId = TicketId,
			BlobContainerName = BlobContainerName,
			BlobName = BlobName,
			OriginalFileName = OriginalFileName,
			ContentType = ContentType,
			SizeBytes = SizeBytes,
			UploadedByOid = UploadedByOid,
			UploadedUtc = UploadedUtc,
			IsDeleted = IsDeleted,
			ETag = ETag.ToString()
		};
}
