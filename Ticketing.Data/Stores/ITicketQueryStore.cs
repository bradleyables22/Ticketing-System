using Ticketing.Data.Models;

namespace Ticketing.Data.Stores;

public interface ITicketQueryStore
{
	IAsyncEnumerable<TicketSummary> GetAssignedAsync(
		string assigneeOid,
		TicketStatus? status = null,
		int? pageSize = null,
		CancellationToken cancellationToken = default);

	Task<PagedResult<TicketSummary>> GetAssignedPageAsync(
		string assigneeOid,
		TicketStatus? status = null,
		int? pageSize = null,
		string? pageToken = null,
		CancellationToken cancellationToken = default);

	IAsyncEnumerable<TicketSummary> GetUnassignedAsync(
		TicketStatus? status = null,
		int? pageSize = null,
		CancellationToken cancellationToken = default);

	Task<PagedResult<TicketSummary>> GetUnassignedPageAsync(
		TicketStatus? status = null,
		int? pageSize = null,
		string? pageToken = null,
		CancellationToken cancellationToken = default);

	IAsyncEnumerable<TicketSummary> GetSubmittedAsync(
		string submitterOid,
		TicketStatus? status = null,
		int? pageSize = null,
		CancellationToken cancellationToken = default);

	Task<PagedResult<TicketSummary>> GetSubmittedPageAsync(
		string submitterOid,
		TicketStatus? status = null,
		int? pageSize = null,
		string? pageToken = null,
		CancellationToken cancellationToken = default);

	IAsyncEnumerable<TicketSummary> GetByStatusAsync(
		TicketStatus status,
		int? pageSize = null,
		CancellationToken cancellationToken = default);

	Task<PagedResult<TicketSummary>> GetByStatusPageAsync(
		TicketStatus status,
		int? pageSize = null,
		string? pageToken = null,
		CancellationToken cancellationToken = default);

	IAsyncEnumerable<TicketSummary> GetByTeamAsync(
		string? teamId,
		TicketStatus? status = null,
		int? pageSize = null,
		CancellationToken cancellationToken = default);

	Task<PagedResult<TicketSummary>> GetByTeamPageAsync(
		string? teamId,
		TicketStatus? status = null,
		int? pageSize = null,
		string? pageToken = null,
		CancellationToken cancellationToken = default);

	IAsyncEnumerable<TicketSummary> GetByQueueAsync(
		string? typeId,
		string? categoryId,
		string? subcategoryId,
		TicketStatus? status = null,
		int? pageSize = null,
		CancellationToken cancellationToken = default);

	Task<PagedResult<TicketSummary>> GetByQueuePageAsync(
		string? typeId,
		string? categoryId,
		string? subcategoryId,
		TicketStatus? status = null,
		int? pageSize = null,
		string? pageToken = null,
		CancellationToken cancellationToken = default);

	IAsyncEnumerable<TicketSummary> GetByTagAsync(
		string tag,
		TicketStatus? status = null,
		int? pageSize = null,
		CancellationToken cancellationToken = default);

	Task<PagedResult<TicketSummary>> GetByTagPageAsync(
		string tag,
		TicketStatus? status = null,
		int? pageSize = null,
		string? pageToken = null,
		CancellationToken cancellationToken = default);
}
