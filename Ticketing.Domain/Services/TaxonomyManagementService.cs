using Ticketing.Data.Models;
using Ticketing.Data.Stores;
using Ticketing.Domain.Exceptions;
using Ticketing.Domain.Models;

namespace Ticketing.Domain.Services;

internal sealed class TaxonomyManagementService : ITaxonomyManagementService
{
	private readonly CurrentUserService _currentUser;
	private readonly ITicketTaxonomyStore _taxonomyStore;
	private readonly ITicketPermissionService _permissions;

	public TaxonomyManagementService(
		CurrentUserService currentUser,
		ITicketTaxonomyStore taxonomyStore,
		ITicketPermissionService permissions)
	{
		_currentUser = currentUser;
		_taxonomyStore = taxonomyStore;
		_permissions = permissions;
	}

	public async Task<TicketTypeRecord> SaveTypeAsync(
		SaveTicketTypeCommand command,
		CancellationToken cancellationToken = default)
	{
		EnsureCanManageTaxonomy();
		var userOid = await _currentUser.RequireUserOidAndSyncProfileAsync(cancellationToken);

		return await _taxonomyStore.SaveTypeAsync(
			new SaveTicketTypeRequest
			{
				TypeId = command.TypeId,
				Name = command.Name,
				Description = command.Description,
				SortOrder = command.SortOrder,
				IsActive = command.IsActive,
				ActorOid = userOid
			},
			cancellationToken);
	}

	public async Task<TicketCategoryRecord> SaveCategoryAsync(
		SaveTicketCategoryCommand command,
		CancellationToken cancellationToken = default)
	{
		EnsureCanManageTaxonomy();
		var userOid = await _currentUser.RequireUserOidAndSyncProfileAsync(cancellationToken);

		return await _taxonomyStore.SaveCategoryAsync(
			new SaveTicketCategoryRequest
			{
				CategoryId = command.CategoryId,
				TypeId = command.TypeId,
				Name = command.Name,
				Description = command.Description,
				SortOrder = command.SortOrder,
				IsActive = command.IsActive,
				ActorOid = userOid
			},
			cancellationToken);
	}

	public async Task<TicketSubcategoryRecord> SaveSubcategoryAsync(
		SaveTicketSubcategoryCommand command,
		CancellationToken cancellationToken = default)
	{
		EnsureCanManageTaxonomy();
		var userOid = await _currentUser.RequireUserOidAndSyncProfileAsync(cancellationToken);

		return await _taxonomyStore.SaveSubcategoryAsync(
			new SaveTicketSubcategoryRequest
			{
				SubcategoryId = command.SubcategoryId,
				TypeId = command.TypeId,
				CategoryId = command.CategoryId,
				Name = command.Name,
				Description = command.Description,
				SortOrder = command.SortOrder,
				IsActive = command.IsActive,
				ActorOid = userOid
			},
			cancellationToken);
	}

	public IAsyncEnumerable<TicketTypeRecord> GetTypesAsync(
		bool includeInactive = false,
		int? pageSize = null,
		CancellationToken cancellationToken = default)
	{
		_currentUser.RequireUserOid();
		return _taxonomyStore.GetTypesAsync(includeInactive, pageSize, cancellationToken);
	}

	public IAsyncEnumerable<TicketCategoryRecord> GetCategoriesAsync(
		string typeId,
		bool includeInactive = false,
		int? pageSize = null,
		CancellationToken cancellationToken = default)
	{
		_currentUser.RequireUserOid();
		return _taxonomyStore.GetCategoriesAsync(typeId, includeInactive, pageSize, cancellationToken);
	}

	public IAsyncEnumerable<TicketSubcategoryRecord> GetSubcategoriesAsync(
		string categoryId,
		bool includeInactive = false,
		int? pageSize = null,
		CancellationToken cancellationToken = default)
	{
		_currentUser.RequireUserOid();
		return _taxonomyStore.GetSubcategoriesAsync(categoryId, includeInactive, pageSize, cancellationToken);
	}

	private void EnsureCanManageTaxonomy()
	{
		_currentUser.RequireUserOid();
		if (!_permissions.CanManageTaxonomy())
		{
			throw new TicketingForbiddenException("Only managers and admins can manage ticket taxonomy.");
		}
	}
}
