using Azure;
using Azure.Data.Tables;
using Ticketing.Data.AzureStorage.Internal;
using Ticketing.Data.Models;

namespace Ticketing.Data.AzureStorage.Entities;

internal sealed class TicketIndexEntity : ITableEntity
{
	public string PartitionKey { get; set; } = string.Empty;

	public string RowKey { get; set; } = string.Empty;

	public DateTimeOffset? Timestamp { get; set; }

	public ETag ETag { get; set; }

	public string TicketId { get; set; } = string.Empty;

	public string TicketPartitionKey { get; set; } = string.Empty;

	public string TicketRowKey { get; set; } = string.Empty;

	public string TicketNumber { get; set; } = string.Empty;

	public string Title { get; set; } = string.Empty;

	public string Status { get; set; } = TicketStatus.Open.ToString();

	public int Priority { get; set; }

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

	public TicketSummary ToSummary() =>
		new()
		{
			TicketId = TicketId,
			TicketNumber = TicketNumber,
			Title = Title,
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
			Tags = StorageKeys.DeserializeTags(TagsJson)
		};

	public static TicketIndexEntity Create(string partitionKey, string rowKey, TicketEntity ticket) =>
		new()
		{
			PartitionKey = partitionKey,
			RowKey = rowKey,
			TicketId = ticket.TicketId,
			TicketPartitionKey = ticket.PartitionKey,
			TicketRowKey = ticket.RowKey,
			TicketNumber = ticket.TicketNumber,
			Title = ticket.Title,
			Status = ticket.Status,
			Priority = ticket.Priority,
			TypeId = ticket.TypeId,
			CategoryId = ticket.CategoryId,
			SubcategoryId = ticket.SubcategoryId,
			SubmitterOid = ticket.SubmitterOid,
			CreatedByOid = string.IsNullOrWhiteSpace(ticket.CreatedByOid) ? ticket.SubmitterOid : ticket.CreatedByOid,
			AssigneeOid = ticket.AssigneeOid,
			AssignedTeamId = ticket.AssignedTeamId,
			OpenedUtc = ticket.OpenedUtc,
			ClosedUtc = ticket.ClosedUtc,
			LastUpdatedUtc = ticket.LastUpdatedUtc,
			TagsJson = ticket.TagsJson
		};
}
