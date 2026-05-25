using Azure;
using Azure.Data.Tables;
using Ticketing.Data.Models;

namespace Ticketing.Data.AzureStorage.Entities;

internal sealed class TeamEntity : ITableEntity
{
	public string PartitionKey { get; set; } = string.Empty;

	public string RowKey { get; set; } = string.Empty;

	public DateTimeOffset? Timestamp { get; set; }

	public ETag ETag { get; set; }

	public string TeamId { get; set; } = string.Empty;

	public string Name { get; set; } = string.Empty;

	public string? Description { get; set; }

	public bool IsActive { get; set; } = true;

	public string CreatedByOid { get; set; } = string.Empty;

	public DateTimeOffset CreatedUtc { get; set; }

	public string? UpdatedByOid { get; set; }

	public DateTimeOffset? UpdatedUtc { get; set; }

	public TeamRecord ToRecord() =>
		new()
		{
			TeamId = TeamId,
			Name = Name,
			Description = Description,
			IsActive = IsActive,
			CreatedByOid = CreatedByOid,
			CreatedUtc = CreatedUtc,
			UpdatedByOid = UpdatedByOid,
			UpdatedUtc = UpdatedUtc,
			ETag = ETag.ToString()
		};
}
