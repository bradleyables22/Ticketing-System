using Ticketing.Data.Models;

namespace Ticketing.Data.Stores;

public interface IUserProfileStore
{
	Task<TicketUserProfile> UpsertAsync(UpsertUserProfileRequest request, CancellationToken cancellationToken = default);

	Task<TicketUserProfile?> GetAsync(string userOid, CancellationToken cancellationToken = default);

	IAsyncEnumerable<TicketUserProfile> SearchAsync(
		string? query,
		bool includeInactive = false,
		int? pageSize = null,
		CancellationToken cancellationToken = default);
}
