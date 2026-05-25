using Ticketing.Data.Models;
using Ticketing.Domain.Models;

namespace Ticketing.Domain.Services;

public interface ITaxonomyManagementService
{
	Task<TicketTypeRecord> SaveTypeAsync(SaveTicketTypeCommand command, CancellationToken cancellationToken = default);

	Task<TicketCategoryRecord> SaveCategoryAsync(SaveTicketCategoryCommand command, CancellationToken cancellationToken = default);

	Task<TicketSubcategoryRecord> SaveSubcategoryAsync(SaveTicketSubcategoryCommand command, CancellationToken cancellationToken = default);

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
