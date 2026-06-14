using System.ComponentModel;
using ModelContextProtocol.Server;
using Ticketing.Auth;
using Ticketing.Data.Models;
using Ticketing.Domain.Models;
using Ticketing.Domain.Services;
using Ticketing.Mcp.Contracts;
using Ticketing.Mcp.Infrastructure;

namespace Ticketing.Mcp.Tools;

[McpServerToolType]
public sealed class TicketingTeamTools
{
	private readonly TicketingMcpAuthorizationService _authorization;
	private readonly ITeamManagementService _teams;

	public TicketingTeamTools(
		TicketingMcpAuthorizationService authorization,
		ITeamManagementService teams)
	{
		_authorization = authorization;
		_teams = teams;
	}

	[McpServerTool(Name = "ticketing_save_team")]
	[Description("Creates a team or updates the requested team id.")]
	public async Task<TicketingMcpToolResult<TeamRecord>> SaveTeamAsync(
		[Description("Team name.")] string name,
		[Description("Optional team id to update. Leave null to create a new team.")] string? teamId = null,
		[Description("Optional team description.")] string? description = null,
		[Description("Whether the team is active for routing and queues.")] bool isActive = true,
		CancellationToken cancellationToken = default)
	{
		return await _authorization.RunAsync(
			TicketingAuthPolicies.ManageTeams,
			ct => _teams.SaveTeamAsync(
				new SaveTeamCommand
				{
					TeamId = teamId,
					Name = name,
					Description = description,
					IsActive = isActive
				},
				ct),
			cancellationToken);
	}

	[McpServerTool(Name = "ticketing_get_team", ReadOnly = true)]
	[Description("Returns a single team definition by team id.")]
	public async Task<TicketingMcpToolResult<TeamRecord>> GetTeamAsync(
		[Description("Opaque team id.")] string teamId,
		CancellationToken cancellationToken = default)
	{
		return await _authorization.RunAsync(
			[TicketingAuthPolicies.Read, TicketingAuthPolicies.WorkTicket],
			ct => _teams.GetTeamAsync(teamId, ct),
			cancellationToken);
	}

	[McpServerTool(Name = "ticketing_list_teams", ReadOnly = true)]
	[Description("Lists teams visible to ticket workers.")]
	public async Task<TicketingMcpToolResult<PagedResult<TeamRecord>>> ListTeamsAsync(
		[Description("Include inactive retired teams when true.")] bool includeInactive = false,
		[Description("Requested page size.")] int? pageSize = null,
		[Description("Continuation token from a previous response's nextPageToken.")] string? pageToken = null,
		CancellationToken cancellationToken = default)
	{
		return await _authorization.RunAsync(
			[TicketingAuthPolicies.Read, TicketingAuthPolicies.WorkTicket],
			ct => _teams.GetTeamsAsync(includeInactive, pageSize, pageToken, ct),
			cancellationToken);
	}

	[McpServerTool(Name = "ticketing_get_my_team_memberships", ReadOnly = true)]
	[Description("Lists team memberships for the authenticated user.")]
	public async Task<TicketingMcpToolResult<PagedResult<TeamMemberRecord>>> GetMyTeamMembershipsAsync(
		[Description("Include inactive memberships when true.")] bool includeInactive = false,
		[Description("Requested page size.")] int? pageSize = null,
		[Description("Continuation token from a previous response's nextPageToken.")] string? pageToken = null,
		CancellationToken cancellationToken = default)
	{
		return await _authorization.RunAsync(
			TicketingAuthPolicies.Read,
			ct => _teams.GetMyMembershipsAsync(includeInactive, pageSize, pageToken, ct),
			cancellationToken);
	}

	[McpServerTool(Name = "ticketing_save_team_member")]
	[Description("Adds a user to a team or updates their team role and active state.")]
	public async Task<TicketingMcpToolResult<TeamMemberRecord>> SaveTeamMemberAsync(
		[Description("Team id.")] string teamId,
		[Description("Microsoft Entra object id for the team member.")] string userOid,
		[Description("Team role. Team roles are domain data, not Entra app roles.")] TeamMemberRole role = TeamMemberRole.Member,
		[Description("Whether the membership is active.")] bool isActive = true,
		CancellationToken cancellationToken = default)
	{
		return await _authorization.RunAsync(
			TicketingAuthPolicies.ManageTeams,
			ct => _teams.SaveMemberAsync(
				new SaveTeamMemberCommand
				{
					TeamId = teamId,
					UserOid = userOid,
					Role = role,
					IsActive = isActive
				},
				ct),
			cancellationToken);
	}

	[McpServerTool(Name = "ticketing_list_team_members", ReadOnly = true)]
	[Description("Lists members for a team.")]
	public async Task<TicketingMcpToolResult<PagedResult<TeamMemberRecord>>> ListTeamMembersAsync(
		[Description("Team id.")] string teamId,
		[Description("Include inactive memberships when true.")] bool includeInactive = false,
		[Description("Requested page size.")] int? pageSize = null,
		[Description("Continuation token from a previous response's nextPageToken.")] string? pageToken = null,
		CancellationToken cancellationToken = default)
	{
		return await _authorization.RunAsync(
			[TicketingAuthPolicies.Read, TicketingAuthPolicies.WorkTicket],
			ct => _teams.GetMembersAsync(teamId, includeInactive, pageSize, pageToken, ct),
			cancellationToken);
	}

	[McpServerTool(Name = "ticketing_save_team_routing")]
	[Description("Creates or updates a routing rule that maps taxonomy, priority, or default fallback to a team.")]
	public async Task<TicketingMcpToolResult<TeamCategoryAssignmentRecord>> SaveTeamRoutingAsync(
		[Description("Team id that should own matching tickets.")] string teamId,
		[Description("Optional routing assignment id to update. Leave null to create a new rule.")] string? assignmentId = null,
		[Description("Optional type id match.")] string? typeId = null,
		[Description("Optional category id match.")] string? categoryId = null,
		[Description("Optional subcategory id match.")] string? subcategoryId = null,
		[Description("Optional priority-specific match.")] TicketPriority? priority = null,
		[Description("Whether this rule is a default fallback.")] bool isDefault = false,
		[Description("Whether this routing rule is active.")] bool isActive = true,
		[Description("Sort order for tie-breaking and administrative display.")] int sortOrder = 0,
		CancellationToken cancellationToken = default)
	{
		return await _authorization.RunAsync(
			TicketingAuthPolicies.ManageTeams,
			ct => _teams.SaveCategoryAssignmentAsync(
				new SaveTeamCategoryAssignmentCommand
				{
					AssignmentId = assignmentId,
					TeamId = teamId,
					TypeId = typeId,
					CategoryId = categoryId,
					SubcategoryId = subcategoryId,
					Priority = priority,
					IsDefault = isDefault,
					IsActive = isActive,
					SortOrder = sortOrder
				},
				ct),
			cancellationToken);
	}

	[McpServerTool(Name = "ticketing_list_team_routing", ReadOnly = true)]
	[Description("Lists taxonomy routing assignments, optionally filtered by team id.")]
	public async Task<TicketingMcpToolResult<PagedResult<TeamCategoryAssignmentRecord>>> ListTeamRoutingAsync(
		[Description("Optional team id filter.")] string? teamId = null,
		[Description("Include inactive routing rules when true.")] bool includeInactive = false,
		[Description("Requested page size.")] int? pageSize = null,
		[Description("Continuation token from a previous response's nextPageToken.")] string? pageToken = null,
		CancellationToken cancellationToken = default)
	{
		return await _authorization.RunAsync(
			TicketingAuthPolicies.ManageTeams,
			ct => _teams.GetCategoryAssignmentsAsync(teamId, includeInactive, pageSize, pageToken, ct),
			cancellationToken);
	}
}
