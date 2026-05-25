using Azure;
using Azure.Data.Tables;
using Ticketing.Data.Models;

namespace Ticketing.Data.AzureStorage.Entities;

internal sealed class TeamCategoryAssignmentEntity : ITableEntity
{
	public string PartitionKey { get; set; } = string.Empty;

	public string RowKey { get; set; } = string.Empty;

	public DateTimeOffset? Timestamp { get; set; }

	public ETag ETag { get; set; }

	public string AssignmentId { get; set; } = string.Empty;

	public string TeamId { get; set; } = string.Empty;

	public string? TypeId { get; set; }

	public string? CategoryId { get; set; }

	public string? SubcategoryId { get; set; }

	public int? Priority { get; set; }

	public bool IsDefault { get; set; }

	public bool IsActive { get; set; } = true;

	public int SortOrder { get; set; }

	public string CreatedByOid { get; set; } = string.Empty;

	public DateTimeOffset CreatedUtc { get; set; }

	public string? UpdatedByOid { get; set; }

	public DateTimeOffset? UpdatedUtc { get; set; }

	public TeamCategoryAssignmentRecord ToRecord() =>
		new()
		{
			AssignmentId = AssignmentId,
			TeamId = TeamId,
			TypeId = TypeId,
			CategoryId = CategoryId,
			SubcategoryId = SubcategoryId,
			Priority = Priority.HasValue ? (TicketPriority)Priority.Value : null,
			IsDefault = IsDefault,
			IsActive = IsActive,
			SortOrder = SortOrder,
			CreatedByOid = CreatedByOid,
			CreatedUtc = CreatedUtc,
			UpdatedByOid = UpdatedByOid,
			UpdatedUtc = UpdatedUtc,
			ETag = ETag.ToString()
		};
}
