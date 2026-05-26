using Ticketing.Domain.Models;

namespace Ticketing.Domain.Services;

public interface ITicketDashboardService
{
	Task<DomainResult<TicketDashboardSummary>> GetSummaryAsync(
		string? teamId = null,
		CancellationToken cancellationToken = default);
}
