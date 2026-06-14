using Ticketing.Data.Models;

namespace Ticketing.Data.Stores;

public interface ITeamStore
{
	Task<TeamRecord> SaveTeamAsync(SaveTeamRequest request, CancellationToken cancellationToken = default);

	Task<TeamMemberRecord> SaveMemberAsync(SaveTeamMemberRequest request, CancellationToken cancellationToken = default);

	Task<TeamCategoryAssignmentRecord> SaveCategoryAssignmentAsync(
		SaveTeamCategoryAssignmentRequest request,
		CancellationToken cancellationToken = default);

	Task<TeamRecord?> GetTeamAsync(string teamId, CancellationToken cancellationToken = default);

	IAsyncEnumerable<TeamRecord> GetTeamsAsync(
		bool includeInactive = false,
		int? pageSize = null,
		CancellationToken cancellationToken = default);

	Task<PagedResult<TeamRecord>> GetTeamsPageAsync(
		bool includeInactive = false,
		int? pageSize = null,
		string? pageToken = null,
		CancellationToken cancellationToken = default);

	IAsyncEnumerable<TeamMemberRecord> GetMembersAsync(
		string teamId,
		bool includeInactive = false,
		int? pageSize = null,
		CancellationToken cancellationToken = default);

	Task<PagedResult<TeamMemberRecord>> GetMembersPageAsync(
		string teamId,
		bool includeInactive = false,
		int? pageSize = null,
		string? pageToken = null,
		CancellationToken cancellationToken = default);

	IAsyncEnumerable<TeamMemberRecord> GetMembershipsForUserAsync(
		string userOid,
		bool includeInactive = false,
		int? pageSize = null,
		CancellationToken cancellationToken = default);

	Task<PagedResult<TeamMemberRecord>> GetMembershipsForUserPageAsync(
		string userOid,
		bool includeInactive = false,
		int? pageSize = null,
		string? pageToken = null,
		CancellationToken cancellationToken = default);

	IAsyncEnumerable<TeamCategoryAssignmentRecord> GetCategoryAssignmentsAsync(
		string? teamId = null,
		bool includeInactive = false,
		int? pageSize = null,
		CancellationToken cancellationToken = default);

	Task<PagedResult<TeamCategoryAssignmentRecord>> GetCategoryAssignmentsPageAsync(
		string? teamId = null,
		bool includeInactive = false,
		int? pageSize = null,
		string? pageToken = null,
		CancellationToken cancellationToken = default);

	Task<TeamRouteResolution?> ResolveTeamAsync(
		string? typeId,
		string? categoryId,
		string? subcategoryId,
		TicketPriority? priority = null,
		CancellationToken cancellationToken = default);

	Task<bool> IsUserOnTeamAsync(string userOid, string teamId, CancellationToken cancellationToken = default);

	Task<bool> IsUserTeamLeadAsync(string userOid, string teamId, CancellationToken cancellationToken = default);
}
