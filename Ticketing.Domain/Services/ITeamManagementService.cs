using Ticketing.Data.Models;
using Ticketing.Domain.Models;

namespace Ticketing.Domain.Services;

public interface ITeamManagementService
{
	Task<TeamRecord> SaveTeamAsync(SaveTeamCommand command, CancellationToken cancellationToken = default);

	Task<TeamMemberRecord> SaveMemberAsync(SaveTeamMemberCommand command, CancellationToken cancellationToken = default);

	Task<TeamCategoryAssignmentRecord> SaveCategoryAssignmentAsync(
		SaveTeamCategoryAssignmentCommand command,
		CancellationToken cancellationToken = default);

	IAsyncEnumerable<TeamRecord> GetTeamsAsync(
		bool includeInactive = false,
		int? pageSize = null,
		CancellationToken cancellationToken = default);

	IAsyncEnumerable<TeamMemberRecord> GetMembersAsync(
		string teamId,
		bool includeInactive = false,
		int? pageSize = null,
		CancellationToken cancellationToken = default);

	IAsyncEnumerable<TeamCategoryAssignmentRecord> GetCategoryAssignmentsAsync(
		string? teamId = null,
		bool includeInactive = false,
		int? pageSize = null,
		CancellationToken cancellationToken = default);
}
