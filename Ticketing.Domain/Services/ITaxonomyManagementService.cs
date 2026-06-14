using Ticketing.Data.Models;
using Ticketing.Domain.Models;

namespace Ticketing.Domain.Services;

public interface ITaxonomyManagementService
{
	Task<DomainResult<TicketTypeRecord>> SaveTypeAsync(SaveTicketTypeCommand command, CancellationToken cancellationToken = default);

	Task<DomainResult<TicketCategoryRecord>> SaveCategoryAsync(SaveTicketCategoryCommand command, CancellationToken cancellationToken = default);

	Task<DomainResult<TicketSubcategoryRecord>> SaveSubcategoryAsync(SaveTicketSubcategoryCommand command, CancellationToken cancellationToken = default);

	Task<DomainResult<PagedResult<TicketTypeRecord>>> GetTypesAsync(
		bool includeInactive = false,
		int? pageSize = null,
		string? pageToken = null,
		CancellationToken cancellationToken = default);

	Task<DomainResult<PagedResult<TicketCategoryRecord>>> GetCategoriesAsync(
		string typeId,
		bool includeInactive = false,
		int? pageSize = null,
		string? pageToken = null,
		CancellationToken cancellationToken = default);

	Task<DomainResult<PagedResult<TicketSubcategoryRecord>>> GetSubcategoriesAsync(
		string categoryId,
		bool includeInactive = false,
		int? pageSize = null,
		string? pageToken = null,
		CancellationToken cancellationToken = default);
}
