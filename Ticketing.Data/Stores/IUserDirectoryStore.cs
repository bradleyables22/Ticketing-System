using Ticketing.Data.Models;

namespace Ticketing.Data.Stores;

public interface IUserDirectoryStore
{
	Task<TicketUserProfile?> GetUserAsync(
		string userOid,
		CancellationToken cancellationToken = default);

	Task<PagedResult<TicketUserProfile>> SearchUsersAsync(
		string? query,
		bool includeInactive = false,
		int? pageSize = null,
		string? pageToken = null,
		CancellationToken cancellationToken = default);
}
