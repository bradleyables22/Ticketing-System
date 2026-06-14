using Ticketing.Data.Models;

namespace Ticketing.Data.Stores;

public interface IUserDirectoryStore
{
	Task<PagedResult<TicketUserProfile>> SearchUsersAsync(
		string? query,
		bool includeInactive = false,
		int? pageSize = null,
		string? pageToken = null,
		CancellationToken cancellationToken = default);
}
