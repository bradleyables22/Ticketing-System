using Azure;
using Azure.Data.Tables;
using Ticketing.Data.Models;

namespace Ticketing.Data.AzureStorage.Entities;

internal sealed class TeamMemberEntity : ITableEntity
{
	public string PartitionKey { get; set; } = string.Empty;

	public string RowKey { get; set; } = string.Empty;

	public DateTimeOffset? Timestamp { get; set; }

	public ETag ETag { get; set; }

	public string TeamId { get; set; } = string.Empty;

	public string UserOid { get; set; } = string.Empty;

	public string Role { get; set; } = TeamMemberRole.Member.ToString();

	public bool IsActive { get; set; } = true;

	public string CreatedByOid { get; set; } = string.Empty;

	public DateTimeOffset CreatedUtc { get; set; }

	public string? UpdatedByOid { get; set; }

	public DateTimeOffset? UpdatedUtc { get; set; }

	public TeamMemberRecord ToRecord() =>
		new()
		{
			TeamId = TeamId,
			UserOid = UserOid,
			Role = Enum.Parse<TeamMemberRole>(Role),
			IsActive = IsActive,
			CreatedByOid = CreatedByOid,
			CreatedUtc = CreatedUtc,
			UpdatedByOid = UpdatedByOid,
			UpdatedUtc = UpdatedUtc,
			ETag = ETag.ToString()
		};

	public TeamMemberEntity CopyForUserProjection() =>
		new()
		{
			PartitionKey = string.Empty,
			RowKey = string.Empty,
			TeamId = TeamId,
			UserOid = UserOid,
			Role = Role,
			IsActive = IsActive,
			CreatedByOid = CreatedByOid,
			CreatedUtc = CreatedUtc,
			UpdatedByOid = UpdatedByOid,
			UpdatedUtc = UpdatedUtc
		};
}
