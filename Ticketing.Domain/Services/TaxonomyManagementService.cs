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

	public Task<DomainResult<TicketTypeRecord>> SaveTypeAsync(
		SaveTicketTypeCommand command,
		CancellationToken cancellationToken = default) =>
		DomainResult<TicketTypeRecord>.TryAsync(async () =>
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
		});

	public Task<DomainResult<TicketCategoryRecord>> SaveCategoryAsync(
		SaveTicketCategoryCommand command,
		CancellationToken cancellationToken = default) =>
		DomainResult<TicketCategoryRecord>.TryAsync(async () =>
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
		});

	public Task<DomainResult<TicketSubcategoryRecord>> SaveSubcategoryAsync(
		SaveTicketSubcategoryCommand command,
		CancellationToken cancellationToken = default) =>
		DomainResult<TicketSubcategoryRecord>.TryAsync(async () =>
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
		});

	public Task<DomainResult<IReadOnlyList<TicketTypeRecord>>> GetTypesAsync(
		bool includeInactive = false,
		int? pageSize = null,
		CancellationToken cancellationToken = default) =>
		DomainResult<IReadOnlyList<TicketTypeRecord>>.TryAsync(async () =>
		{
			_currentUser.RequireUserOid();
			return await _taxonomyStore.GetTypesAsync(CanIncludeInactive(includeInactive), pageSize, cancellationToken)
				.ToReadOnlyListAsync(cancellationToken);
		});

	public Task<DomainResult<IReadOnlyList<TicketCategoryRecord>>> GetCategoriesAsync(
		string typeId,
		bool includeInactive = false,
		int? pageSize = null,
		CancellationToken cancellationToken = default) =>
		DomainResult<IReadOnlyList<TicketCategoryRecord>>.TryAsync(async () =>
		{
			_currentUser.RequireUserOid();
			return await _taxonomyStore.GetCategoriesAsync(typeId, CanIncludeInactive(includeInactive), pageSize, cancellationToken)
				.ToReadOnlyListAsync(cancellationToken);
		});

	public Task<DomainResult<IReadOnlyList<TicketSubcategoryRecord>>> GetSubcategoriesAsync(
		string categoryId,
		bool includeInactive = false,
		int? pageSize = null,
		CancellationToken cancellationToken = default) =>
		DomainResult<IReadOnlyList<TicketSubcategoryRecord>>.TryAsync(async () =>
		{
			_currentUser.RequireUserOid();
			return await _taxonomyStore.GetSubcategoriesAsync(categoryId, CanIncludeInactive(includeInactive), pageSize, cancellationToken)
				.ToReadOnlyListAsync(cancellationToken);
		});

	private void EnsureCanManageTaxonomy()
	{
		_currentUser.RequireUserOid();
		if (!_permissions.CanManageTaxonomy())
		{
			throw new TicketingForbiddenException("Only managers and admins can manage ticket taxonomy.");
		}
	}

	private bool CanIncludeInactive(bool includeInactive) =>
		includeInactive && _permissions.CanManageTaxonomy();
}
