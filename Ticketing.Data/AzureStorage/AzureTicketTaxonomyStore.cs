using System.Runtime.CompilerServices;
using Azure.Data.Tables;
using Ticketing.Data.AzureStorage.Entities;
using Ticketing.Data.AzureStorage.Internal;
using Ticketing.Data.Models;
using Ticketing.Data.Stores;

namespace Ticketing.Data.AzureStorage;

internal sealed class AzureTicketTaxonomyStore : ITicketTaxonomyStore
{
	private const string TypeKind = "Type";
	private const string CategoryKind = "Category";
	private const string SubcategoryKind = "Subcategory";

	private readonly AzureStorageClients _clients;

	public AzureTicketTaxonomyStore(AzureStorageClients clients)
	{
		_clients = clients;
	}

	public async Task<TicketTypeRecord> SaveTypeAsync(SaveTicketTypeRequest request, CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(request.Name);
		ArgumentException.ThrowIfNullOrWhiteSpace(request.ActorOid);

		var typeId = string.IsNullOrWhiteSpace(request.TypeId) ? StorageKeys.NewId() : request.TypeId.Trim();
		var entity = await GetOrCreateAsync(
			StorageKeys.TypePartition(),
			StorageKeys.TypeRow(typeId),
			typeId,
			TypeKind,
			request.ActorOid,
			cancellationToken);

		ApplyCommon(entity, request.Name, request.Description, request.SortOrder, request.IsActive, request.ActorOid);
		await _clients.TicketTaxonomy.UpsertEntityAsync(entity, TableUpdateMode.Replace, cancellationToken);
		return entity.ToTypeRecord();
	}

	public async Task<TicketCategoryRecord> SaveCategoryAsync(SaveTicketCategoryRequest request, CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(request.TypeId);
		ArgumentException.ThrowIfNullOrWhiteSpace(request.Name);
		ArgumentException.ThrowIfNullOrWhiteSpace(request.ActorOid);

		var categoryId = string.IsNullOrWhiteSpace(request.CategoryId) ? StorageKeys.NewId() : request.CategoryId.Trim();
		var entity = await GetOrCreateAsync(
			StorageKeys.CategoryPartition(request.TypeId),
			StorageKeys.CategoryRow(categoryId),
			categoryId,
			CategoryKind,
			request.ActorOid,
			cancellationToken);

		entity.TypeId = request.TypeId.Trim();
		ApplyCommon(entity, request.Name, request.Description, request.SortOrder, request.IsActive, request.ActorOid);
		await _clients.TicketTaxonomy.UpsertEntityAsync(entity, TableUpdateMode.Replace, cancellationToken);
		return entity.ToCategoryRecord();
	}

	public async Task<TicketSubcategoryRecord> SaveSubcategoryAsync(SaveTicketSubcategoryRequest request, CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(request.TypeId);
		ArgumentException.ThrowIfNullOrWhiteSpace(request.CategoryId);
		ArgumentException.ThrowIfNullOrWhiteSpace(request.Name);
		ArgumentException.ThrowIfNullOrWhiteSpace(request.ActorOid);

		var subcategoryId = string.IsNullOrWhiteSpace(request.SubcategoryId) ? StorageKeys.NewId() : request.SubcategoryId.Trim();
		var entity = await GetOrCreateAsync(
			StorageKeys.SubcategoryPartition(request.CategoryId),
			StorageKeys.SubcategoryRow(subcategoryId),
			subcategoryId,
			SubcategoryKind,
			request.ActorOid,
			cancellationToken);

		entity.TypeId = request.TypeId.Trim();
		entity.CategoryId = request.CategoryId.Trim();
		ApplyCommon(entity, request.Name, request.Description, request.SortOrder, request.IsActive, request.ActorOid);
		await _clients.TicketTaxonomy.UpsertEntityAsync(entity, TableUpdateMode.Replace, cancellationToken);
		return entity.ToSubcategoryRecord();
	}

	public async IAsyncEnumerable<TicketTypeRecord> GetTypesAsync(
		bool includeInactive = false,
		int? pageSize = null,
		[EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		var returned = 0;
		await foreach (var entity in QueryPartitionAsync(StorageKeys.TypePartition(), pageSize, cancellationToken))
		{
			if (entity.TaxonomyKind == TypeKind && (includeInactive || entity.IsActive))
			{
				yield return entity.ToTypeRecord();
				returned++;
				if (AzureTablePageLimits.IsFull(pageSize, returned))
				{
					yield break;
				}
			}
		}
	}

	public async IAsyncEnumerable<TicketCategoryRecord> GetCategoriesAsync(
		string typeId,
		bool includeInactive = false,
		int? pageSize = null,
		[EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(typeId);

		var returned = 0;
		await foreach (var entity in QueryPartitionAsync(StorageKeys.CategoryPartition(typeId), pageSize, cancellationToken))
		{
			if (entity.TaxonomyKind == CategoryKind && (includeInactive || entity.IsActive))
			{
				yield return entity.ToCategoryRecord();
				returned++;
				if (AzureTablePageLimits.IsFull(pageSize, returned))
				{
					yield break;
				}
			}
		}
	}

	public async IAsyncEnumerable<TicketSubcategoryRecord> GetSubcategoriesAsync(
		string categoryId,
		bool includeInactive = false,
		int? pageSize = null,
		[EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(categoryId);

		var returned = 0;
		await foreach (var entity in QueryPartitionAsync(StorageKeys.SubcategoryPartition(categoryId), pageSize, cancellationToken))
		{
			if (entity.TaxonomyKind == SubcategoryKind && (includeInactive || entity.IsActive))
			{
				yield return entity.ToSubcategoryRecord();
				returned++;
				if (AzureTablePageLimits.IsFull(pageSize, returned))
				{
					yield break;
				}
			}
		}
	}

	private async Task<TicketTaxonomyEntity> GetOrCreateAsync(
		string partitionKey,
		string rowKey,
		string itemId,
		string kind,
		string actorOid,
		CancellationToken cancellationToken)
	{
		var response = await _clients.TicketTaxonomy.GetEntityIfExistsAsync<TicketTaxonomyEntity>(
			partitionKey,
			rowKey,
			cancellationToken: cancellationToken);

		if (response.HasValue)
		{
			return response.Value!;
		}

		var now = DateTimeOffset.UtcNow;
		return new TicketTaxonomyEntity
		{
			PartitionKey = partitionKey,
			RowKey = rowKey,
			TaxonomyKind = kind,
			ItemId = itemId,
			CreatedByOid = actorOid.Trim(),
			CreatedUtc = now
		};
	}

	private async IAsyncEnumerable<TicketTaxonomyEntity> QueryPartitionAsync(
		string partitionKey,
		int? pageSize,
		[EnumeratorCancellation] CancellationToken cancellationToken)
	{
		var normalizedPageSize = AzureTablePageLimits.Normalize(pageSize);
		var filter = TableClient.CreateQueryFilter($"PartitionKey eq {partitionKey}");

		await foreach (var entity in _clients.TicketTaxonomy
			.QueryAsync<TicketTaxonomyEntity>(filter, maxPerPage: normalizedPageSize, cancellationToken: cancellationToken)
			.ConfigureAwait(false))
		{
			yield return entity;
		}
	}

	private static void ApplyCommon(
		TicketTaxonomyEntity entity,
		string name,
		string? description,
		int sortOrder,
		bool isActive,
		string actorOid)
	{
		entity.Name = name.Trim();
		entity.Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
		entity.SortOrder = sortOrder;
		entity.IsActive = isActive;
		entity.UpdatedByOid = actorOid.Trim();
		entity.UpdatedUtc = DateTimeOffset.UtcNow;
	}
}
