using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Ticketing.Data.Models;

namespace Ticketing.Data.AzureStorage.Internal;

internal static class StorageKeys
{
	private const int TicketBucketCount = 32;

	public const string UnassignedAssignee = "unassigned";

	public const string UnassignedTeam = "unassigned";

	public static string NewId() => Guid.CreateVersion7().ToString("N");

	public static string TicketPartition(string ticketId) => $"TICKET|{TicketBucket(ticketId)}";

	public static string TicketRow(string ticketId) => ticketId;

	public static string TicketLookupPartition() => "NUMBER";

	public static string TicketLookupRow(string ticketNumber) => EncodeKeySegment(ticketNumber.Trim().ToUpperInvariant());

	public static string TicketScopedPartition(string ticketId) => $"TICKET|{ticketId}";

	public static string NoteRow(DateTimeOffset createdUtc, string noteId) => $"NOTE|{ReverseTicks(createdUtc)}|{noteId}";

	public static string AuditRow(DateTimeOffset createdUtc, string eventId) => $"EVENT|{ReverseTicks(createdUtc)}|{eventId}";

	public static string AttachmentRow(DateTimeOffset uploadedUtc, string attachmentId) => $"ATTACH|{ReverseTicks(uploadedUtc)}|{attachmentId}";

	public static string AssigneePartition(string? assigneeOid, TicketStatus status)
	{
		var assigneeSegment = string.IsNullOrWhiteSpace(assigneeOid)
			? UnassignedAssignee
			: EncodeKeySegment(assigneeOid);

		return $"ASSIGNEE|{assigneeSegment}|STATUS|{StatusSegment(status)}";
	}

	public static string SubmitterPartition(string submitterOid, TicketStatus status) =>
		$"SUBMITTER|{EncodeKeySegment(submitterOid)}|STATUS|{StatusSegment(status)}";

	public static string StatusPartition(TicketStatus status) => $"STATUS|{StatusSegment(status)}";

	public static string QueuePartition(string? typeId, string? categoryId, string? subcategoryId, TicketStatus status) =>
		$"{QueuePartitionWithoutStatus(typeId, categoryId, subcategoryId)}|STATUS|{StatusSegment(status)}";

	public static string QueuePartitionWithoutStatus(string? typeId, string? categoryId, string? subcategoryId)
	{
		if (!string.IsNullOrWhiteSpace(subcategoryId))
		{
			return $"QUEUE|SUBCATEGORY|{EncodeKeySegment(subcategoryId)}";
		}

		if (!string.IsNullOrWhiteSpace(categoryId))
		{
			return $"QUEUE|CATEGORY|{EncodeKeySegment(categoryId)}";
		}

		if (!string.IsNullOrWhiteSpace(typeId))
		{
			return $"QUEUE|TYPE|{EncodeKeySegment(typeId)}";
		}

		return "QUEUE|UNCLASSIFIED";
	}

	public static string TagPartition(string tag, TicketStatus status) =>
		$"TAG|{EncodeKeySegment(NormalizeTag(tag))}|STATUS|{StatusSegment(status)}";

	public static string TeamQueuePartition(string? teamId, TicketStatus status)
	{
		var teamSegment = string.IsNullOrWhiteSpace(teamId)
			? UnassignedTeam
			: EncodeKeySegment(teamId);

		return $"TEAM|{teamSegment}|STATUS|{StatusSegment(status)}";
	}

	public static string IndexRow(DateTimeOffset lastUpdatedUtc, string ticketId) =>
		$"UPDATED|{ReverseTicks(lastUpdatedUtc)}|TICKET|{ticketId}";

	public static string UserProfilePartition() => "USER";

	public static string UserProfileRow(string userOid) => EncodeKeySegment(userOid);

	public static string TeamDefinitionPartition() => "TEAM";

	public static string TeamDefinitionRow(string teamId) => $"TEAM|{EncodeKeySegment(teamId)}";

	public static string TeamMemberByTeamPartition(string teamId) => $"TEAM|{EncodeKeySegment(teamId)}";

	public static string TeamMemberByTeamRow(string userOid) => $"USER|{EncodeKeySegment(userOid)}";

	public static string TeamMemberByUserPartition(string userOid) => $"USER|{EncodeKeySegment(userOid)}";

	public static string TeamMemberByUserRow(string teamId) => $"TEAM|{EncodeKeySegment(teamId)}";

	public static string TeamRouteTypePartition(string typeId, TicketPriority? priority) =>
		$"ROUTE|TYPE|{EncodeKeySegment(typeId)}|PRIORITY|{PrioritySegment(priority)}";

	public static string TeamRouteCategoryPartition(string categoryId, TicketPriority? priority) =>
		$"ROUTE|CATEGORY|{EncodeKeySegment(categoryId)}|PRIORITY|{PrioritySegment(priority)}";

	public static string TeamRouteSubcategoryPartition(string subcategoryId, TicketPriority? priority) =>
		$"ROUTE|SUBCATEGORY|{EncodeKeySegment(subcategoryId)}|PRIORITY|{PrioritySegment(priority)}";

	public static string TeamRouteDefaultPartition(TicketPriority? priority) =>
		$"ROUTE|DEFAULT|PRIORITY|{PrioritySegment(priority)}";

	public static string TeamRouteRow(string assignmentId) => $"ASSIGNMENT|{EncodeKeySegment(assignmentId)}";

	public static string TypePartition() => "TYPE";

	public static string TypeRow(string typeId) => $"TYPE|{EncodeKeySegment(typeId)}";

	public static string CategoryPartition(string typeId) => $"TYPE|{EncodeKeySegment(typeId)}";

	public static string CategoryRow(string categoryId) => $"CATEGORY|{EncodeKeySegment(categoryId)}";

	public static string SubcategoryPartition(string categoryId) => $"CATEGORY|{EncodeKeySegment(categoryId)}";

	public static string SubcategoryRow(string subcategoryId) => $"SUBCATEGORY|{EncodeKeySegment(subcategoryId)}";

	public static string SerializeTags(IEnumerable<string> tags) =>
		JsonSerializer.Serialize(NormalizeTags(tags));

	public static IReadOnlyList<string> DeserializeTags(string? tagsJson)
	{
		if (string.IsNullOrWhiteSpace(tagsJson))
		{
			return Array.Empty<string>();
		}

		try
		{
			return JsonSerializer.Deserialize<string[]>(tagsJson) ?? Array.Empty<string>();
		}
		catch (JsonException)
		{
			return tagsJson.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
		}
	}

	public static IReadOnlyList<string> NormalizeTags(IEnumerable<string> tags) =>
		tags
			.Select(NormalizeTag)
			.Where(tag => tag.Length > 0)
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.Order(StringComparer.OrdinalIgnoreCase)
			.ToArray();

	public static string NormalizeTag(string? tag) => tag?.Trim().ToLowerInvariant() ?? string.Empty;

	public static string SafeBlobFileName(string fileName)
	{
		var safe = Path.GetFileName(fileName);
		return string.IsNullOrWhiteSpace(safe) ? "attachment" : safe;
	}

	public static string AttachmentBlobName(string ticketId, string attachmentId, string fileName) =>
		$"tickets/{ticketId}/attachments/{attachmentId}/{SafeBlobFileName(fileName)}";

	private static string StatusSegment(TicketStatus status) => status.ToString().ToUpperInvariant();

	private static string PrioritySegment(TicketPriority? priority) =>
		priority.HasValue ? priority.Value.ToString().ToUpperInvariant() : "ANY";

	private static string TicketBucket(string ticketId)
	{
		var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(ticketId));
		return (bytes[0] % TicketBucketCount).ToString("D2");
	}

	private static string ReverseTicks(DateTimeOffset value)
	{
		var reverseTicks = DateTimeOffset.MaxValue.UtcTicks - value.ToUniversalTime().Ticks;
		return reverseTicks.ToString("D19");
	}

	private static string EncodeKeySegment(string value)
	{
		var bytes = Encoding.UTF8.GetBytes(value.Trim());
		return Convert.ToBase64String(bytes)
			.TrimEnd('=')
			.Replace('+', '-')
			.Replace('/', '_');
	}
}
