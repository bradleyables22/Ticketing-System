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

	Task<DomainResult<IReadOnlyList<TeamRecord>>> GetTeamsAsync(
		bool includeInactive = false,
		int? pageSize = null,
		CancellationToken cancellationToken = default);

	Task<DomainResult<IReadOnlyList<TeamMemberRecord>>> GetMyMembershipsAsync(
		bool includeInactive = false,
		int? pageSize = null,
		CancellationToken cancellationToken = default);

	Task<DomainResult<IReadOnlyList<TeamMemberRecord>>> GetMembersAsync(
		string teamId,
		bool includeInactive = false,
		int? pageSize = null,
		CancellationToken cancellationToken = default);

	Task<DomainResult<IReadOnlyList<TeamCategoryAssignmentRecord>>> GetCategoryAssignmentsAsync(
		string? teamId = null,
		bool includeInactive = false,
		int? pageSize = null,
		CancellationToken cancellationToken = default);
}
