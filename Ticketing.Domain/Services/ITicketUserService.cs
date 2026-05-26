using Ticketing.Data.Models;
using Ticketing.Domain.Models;

namespace Ticketing.Domain.Services;

public interface ITicketUserService
{
	Task<DomainResult<TicketingCurrentUserContext>> GetCurrentAsync(CancellationToken cancellationToken = default);

	Task<DomainResult<TicketUserProfile>> GetUserAsync(string userOid, CancellationToken cancellationToken = default);

	Task<DomainResult<IReadOnlyList<TicketUserProfile>>> SearchUsersAsync(
		string? query,
		bool includeInactive = false,
		int? pageSize = null,
		CancellationToken cancellationToken = default);
}
