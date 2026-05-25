using Azure;
using Azure.Data.Tables;
using Ticketing.Data.Models;

namespace Ticketing.Data.AzureStorage.Entities;

internal sealed class UserProfileEntity : ITableEntity
{
	public string PartitionKey { get; set; } = string.Empty;

	public string RowKey { get; set; } = string.Empty;

	public DateTimeOffset? Timestamp { get; set; }

	public ETag ETag { get; set; }

	public string UserOid { get; set; } = string.Empty;

	public string DisplayName { get; set; } = string.Empty;

	public string? Email { get; set; }

	public string? Department { get; set; }

	public string? JobTitle { get; set; }

	public bool IsActive { get; set; } = true;

	public DateTimeOffset LastSeenUtc { get; set; }

	public TicketUserProfile ToRecord() =>
		new()
		{
			UserOid = UserOid,
			DisplayName = DisplayName,
			Email = Email,
			Department = Department,
			JobTitle = JobTitle,
			IsActive = IsActive,
			LastSeenUtc = LastSeenUtc,
			ETag = ETag.ToString()
		};
}
