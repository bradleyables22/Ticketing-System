using Azure;
using Azure.Data.Tables;
using Ticketing.Data.Models;

namespace Ticketing.Data.AzureStorage.Entities;

internal sealed class TicketTaxonomyEntity : ITableEntity
{
	public string PartitionKey { get; set; } = string.Empty;

	public string RowKey { get; set; } = string.Empty;

	public DateTimeOffset? Timestamp { get; set; }

	public ETag ETag { get; set; }

	public string TaxonomyKind { get; set; } = string.Empty;

	public string ItemId { get; set; } = string.Empty;

	public string? TypeId { get; set; }

	public string? CategoryId { get; set; }

	public string Name { get; set; } = string.Empty;

	public string? Description { get; set; }

	public int SortOrder { get; set; }

	public bool IsActive { get; set; } = true;

	public string CreatedByOid { get; set; } = string.Empty;

	public DateTimeOffset CreatedUtc { get; set; }

	public string? UpdatedByOid { get; set; }

	public DateTimeOffset? UpdatedUtc { get; set; }

	public TicketTypeRecord ToTypeRecord() =>
		new()
		{
			TypeId = ItemId,
			Name = Name,
			Description = Description,
			SortOrder = SortOrder,
			IsActive = IsActive,
			CreatedByOid = CreatedByOid,
			CreatedUtc = CreatedUtc,
			UpdatedByOid = UpdatedByOid,
			UpdatedUtc = UpdatedUtc,
			ETag = ETag.ToString()
		};

	public TicketCategoryRecord ToCategoryRecord() =>
		new()
		{
			CategoryId = ItemId,
			TypeId = TypeId ?? string.Empty,
			Name = Name,
			Description = Description,
			SortOrder = SortOrder,
			IsActive = IsActive,
			CreatedByOid = CreatedByOid,
			CreatedUtc = CreatedUtc,
			UpdatedByOid = UpdatedByOid,
			UpdatedUtc = UpdatedUtc,
			ETag = ETag.ToString()
		};

	public TicketSubcategoryRecord ToSubcategoryRecord() =>
		new()
		{
			SubcategoryId = ItemId,
			CategoryId = CategoryId ?? string.Empty,
			TypeId = TypeId ?? string.Empty,
			Name = Name,
			Description = Description,
			SortOrder = SortOrder,
			IsActive = IsActive,
			CreatedByOid = CreatedByOid,
			CreatedUtc = CreatedUtc,
			UpdatedByOid = UpdatedByOid,
			UpdatedUtc = UpdatedUtc,
			ETag = ETag.ToString()
		};
}
