using Azure;
using Azure.Data.Tables;
using Ticketing.Data.AzureStorage.Internal;
using Ticketing.Data.Models;

namespace Ticketing.Data.AzureStorage.Entities;

internal sealed class TicketEntity : ITableEntity
{
	public string PartitionKey { get; set; } = string.Empty;

	public string RowKey { get; set; } = string.Empty;

	public DateTimeOffset? Timestamp { get; set; }

	public ETag ETag { get; set; }

	public string TicketId { get; set; } = string.Empty;

	public string TicketNumber { get; set; } = string.Empty;

	public string Title { get; set; } = string.Empty;

	public string Description { get; set; } = string.Empty;

	public string Status { get; set; } = TicketStatus.Open.ToString();

	public int Priority { get; set; } = (int)TicketPriority.Normal;

	public string? TypeId { get; set; }

	public string? CategoryId { get; set; }

	public string? SubcategoryId { get; set; }

	public string SubmitterOid { get; set; } = string.Empty;

	public string CreatedByOid { get; set; } = string.Empty;

	public string? AssigneeOid { get; set; }

	public string? AssignedTeamId { get; set; }

	public DateTimeOffset OpenedUtc { get; set; }

	public DateTimeOffset? ClosedUtc { get; set; }

	public DateTimeOffset LastUpdatedUtc { get; set; }

	public string TagsJson { get; set; } = "[]";

	public int NoteCount { get; set; }

	public int AttachmentCount { get; set; }

	public TicketEntity Copy() =>
		new()
		{
			PartitionKey = PartitionKey,
			RowKey = RowKey,
			Timestamp = Timestamp,
			ETag = ETag,
			TicketId = TicketId,
			TicketNumber = TicketNumber,
			Title = Title,
			Description = Description,
			Status = Status,
			Priority = Priority,
			TypeId = TypeId,
			CategoryId = CategoryId,
			SubcategoryId = SubcategoryId,
			SubmitterOid = SubmitterOid,
			CreatedByOid = CreatedByOid,
			AssigneeOid = AssigneeOid,
			AssignedTeamId = AssignedTeamId,
			OpenedUtc = OpenedUtc,
			ClosedUtc = ClosedUtc,
			LastUpdatedUtc = LastUpdatedUtc,
			TagsJson = TagsJson,
			NoteCount = NoteCount,
			AttachmentCount = AttachmentCount
		};

	public TicketRecord ToRecord() =>
		new()
		{
			TicketId = TicketId,
			TicketNumber = TicketNumber,
			Title = Title,
			Description = Description,
			Status = Enum.Parse<TicketStatus>(Status),
			Priority = (TicketPriority)Priority,
			TypeId = TypeId,
			CategoryId = CategoryId,
			SubcategoryId = SubcategoryId,
			SubmitterOid = SubmitterOid,
			CreatedByOid = string.IsNullOrWhiteSpace(CreatedByOid) ? SubmitterOid : CreatedByOid,
			AssigneeOid = AssigneeOid,
			AssignedTeamId = AssignedTeamId,
			OpenedUtc = OpenedUtc,
			ClosedUtc = ClosedUtc,
			LastUpdatedUtc = LastUpdatedUtc,
			Tags = StorageKeys.DeserializeTags(TagsJson),
			NoteCount = NoteCount,
			AttachmentCount = AttachmentCount,
			ETag = ETag.ToString()
		};

	public static TicketEntity FromCreateRequest(CreateTicketRequest request, DateTimeOffset openedUtc, string? assignedTeamId)
	{
		var ticketId = StorageKeys.NewId();
		var ticketNumber = TicketNumberGenerator.Create(openedUtc, ticketId);

		return new TicketEntity
		{
			PartitionKey = StorageKeys.TicketPartition(ticketId),
			RowKey = StorageKeys.TicketRow(ticketId),
			TicketId = ticketId,
			TicketNumber = ticketNumber,
			Title = request.Title.Trim(),
			Description = request.Description,
			Status = TicketStatus.Open.ToString(),
			Priority = (int)request.Priority,
			TypeId = NormalizeOptional(request.TypeId),
			CategoryId = NormalizeOptional(request.CategoryId),
			SubcategoryId = NormalizeOptional(request.SubcategoryId),
			SubmitterOid = request.SubmitterOid.Trim(),
			CreatedByOid = request.CreatedByOid.Trim(),
			AssigneeOid = NormalizeOptional(request.AssigneeOid),
			AssignedTeamId = NormalizeOptional(assignedTeamId),
			OpenedUtc = openedUtc,
			LastUpdatedUtc = openedUtc,
			TagsJson = StorageKeys.SerializeTags(request.Tags)
		};
	}

	private static string? NormalizeOptional(string? value) =>
		string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
