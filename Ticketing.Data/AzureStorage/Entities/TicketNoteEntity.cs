using Azure;
using Azure.Data.Tables;
using Ticketing.Data.Models;

namespace Ticketing.Data.AzureStorage.Entities;

internal sealed class TicketNoteEntity : ITableEntity
{
	public string PartitionKey { get; set; } = string.Empty;

	public string RowKey { get; set; } = string.Empty;

	public DateTimeOffset? Timestamp { get; set; }

	public ETag ETag { get; set; }

	public string NoteId { get; set; } = string.Empty;

	public string TicketId { get; set; } = string.Empty;

	public string AuthorOid { get; set; } = string.Empty;

	public bool IsInternal { get; set; }

	public string Body { get; set; } = string.Empty;

	public DateTimeOffset CreatedUtc { get; set; }

	public DateTimeOffset? EditedUtc { get; set; }

	public TicketNoteRecord ToRecord() =>
		new()
		{
			NoteId = NoteId,
			TicketId = TicketId,
			AuthorOid = AuthorOid,
			IsInternal = IsInternal,
			Body = Body,
			CreatedUtc = CreatedUtc,
			EditedUtc = EditedUtc,
			ETag = ETag.ToString()
		};
}
