using Ticketing.Data.Models;
using Ticketing.Domain.Models;

namespace Ticketing.Domain.Services;

public interface ITeamManagementService
{
	Task<DomainResult<TeamRecord>> SaveTeamAsync(SaveTeamCommand command, CancellationToken cancellationToken = default);

	Task<DomainResult<TeamMemberRecord>> SaveMemberAsync(SaveTeamMemberCommand command, CancellationToken cancellationToken = default);

	Task<DomainResult<TeamCategoryAssignmentRecord>> SaveCategoryAssignmentAsync(
		SaveTeamCategoryAssignmentCommand command,
		CancellationToken cancellationToken = default);

	Task<DomainResult<TeamRecord>> GetTeamAsync(string teamId, CancellationToken cancellationToken = default);

	Task<DomainResult<PagedResult<TeamRecord>>> GetTeamsAsync(
		bool includeInactive = false,
		int? pageSize = null,
		string? pageToken = null,
		CancellationToken cancellationToken = default);

	Task<DomainResult<PagedResult<TeamMemberRecord>>> GetMyMembershipsAsync(
		bool includeInactive = false,
		int? pageSize = null,
		string? pageToken = null,
		CancellationToken cancellationToken = default);

	Task<DomainResult<PagedResult<TeamMemberRecord>>> GetMembersAsync(
		string teamId,
		bool includeInactive = false,
		int? pageSize = null,
		string? pageToken = null,
		CancellationToken cancellationToken = default);

	Task<DomainResult<PagedResult<TeamCategoryAssignmentRecord>>> GetCategoryAssignmentsAsync(
		string? teamId = null,
		bool includeInactive = false,
		int? pageSize = null,
		string? pageToken = null,
		CancellationToken cancellationToken = default);
}
