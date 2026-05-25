using Ticketing.Data.Models;

namespace Ticketing.Data.Stores;

public interface ITicketTaxonomyStore
{
	Task<TicketTypeRecord> SaveTypeAsync(SaveTicketTypeRequest request, CancellationToken cancellationToken = default);

	Task<TicketCategoryRecord> SaveCategoryAsync(SaveTicketCategoryRequest request, CancellationToken cancellationToken = default);

	Task<TicketSubcategoryRecord> SaveSubcategoryAsync(SaveTicketSubcategoryRequest request, CancellationToken cancellationToken = default);

	IAsyncEnumerable<TicketTypeRecord> GetTypesAsync(
		bool includeInactive = false,
		int? pageSize = null,
		CancellationToken cancellationToken = default);

	IAsyncEnumerable<TicketCategoryRecord> GetCategoriesAsync(
		string typeId,
		bool includeInactive = false,
		int? pageSize = null,
		CancellationToken cancellationToken = default);

	IAsyncEnumerable<TicketSubcategoryRecord> GetSubcategoriesAsync(
		string categoryId,
		bool includeInactive = false,
		int? pageSize = null,
		CancellationToken cancellationToken = default);
}
