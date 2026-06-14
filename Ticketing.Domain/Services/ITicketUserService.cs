using Ticketing.Data.Models;
using Ticketing.Domain.Models;

namespace Ticketing.Domain.Services;

public interface ITicketUserService
{
	Task<DomainResult<TicketingCurrentUserContext>> GetCurrentAsync(CancellationToken cancellationToken = default);

	Task<DomainResult<TicketUserProfile>> GetUserAsync(string userOid, CancellationToken cancellationToken = default);

	Task<DomainResult<PagedResult<TicketUserProfile>>> SearchUsersAsync(
		string? query,
		bool includeInactive = false,
		int? pageSize = null,
		string? pageToken = null,
		CancellationToken cancellationToken = default);
}
