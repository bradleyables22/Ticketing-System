using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Ticketing.Auth;
using Ticketing.Domain.Models;
using Ticketing.Domain.Services;
using Ticketing.Rest.Contracts;
using Ticketing.Rest.Infrastructure;

namespace Ticketing.Rest.Endpoints;

internal static class TeamEndpoints
{
	public static RouteGroupBuilder MapTeamEndpoints(this RouteGroupBuilder api)
	{
		var teams = api.MapGroup("/teams")
			.WithTags("Teams")
			.RequireAuthorization(TicketingAuthPolicies.Read);

		teams.MapPost("/", async (
				SaveTeamHttpRequest request,
				ITeamManagementService teamManagement,
				CancellationToken cancellationToken) =>
			{
				var result = await teamManagement.SaveTeamAsync(
					new SaveTeamCommand
					{
						TeamId = request.TeamId,
						Name = request.Name,
						Description = request.Description,
						IsActive = request.IsActive
					},
					cancellationToken);

				return DomainHttpResultMapper.ToCreated(result, team => $"/api/teams/{team.TeamId}");
			})
			.RequireAuthorization(TicketingAuthPolicies.ManageTeams)
			.WithName("SaveTeam");

		teams.MapPut("/{teamId}", async (
				string teamId,
				SaveTeamHttpRequest request,
				ITeamManagementService teamManagement,
				CancellationToken cancellationToken) =>
			{
				var result = await teamManagement.SaveTeamAsync(
					new SaveTeamCommand
					{
						TeamId = teamId,
						Name = request.Name,
						Description = request.Description,
						IsActive = request.IsActive
					},
					cancellationToken);

				return DomainHttpResultMapper.ToResult(result);
			})
			.RequireAuthorization(TicketingAuthPolicies.ManageTeams)
			.WithName("UpdateTeam");

		teams.MapGet("/", async (
				bool includeInactive,
				int? pageSize,
				string? pageToken,
				ITeamManagementService teamManagement,
				CancellationToken cancellationToken) =>
			{
				var result = await teamManagement.GetTeamsAsync(includeInactive, pageSize, pageToken, cancellationToken);
				return DomainHttpResultMapper.ToResult(result);
			})
			.RequireAuthorization(TicketingAuthPolicies.WorkTicket)
			.WithName("GetTeams");

		teams.MapGet("/my-memberships", async (
				bool includeInactive,
				int? pageSize,
				string? pageToken,
				ITeamManagementService teamManagement,
				CancellationToken cancellationToken) =>
			{
				var result = await teamManagement.GetMyMembershipsAsync(includeInactive, pageSize, pageToken, cancellationToken);
				return DomainHttpResultMapper.ToResult(result);
			})
			.WithName("GetMyTeamMemberships");

		teams.MapGet("/{teamId}", async (
				string teamId,
				ITeamManagementService teamManagement,
				CancellationToken cancellationToken) =>
			{
				var result = await teamManagement.GetTeamAsync(teamId, cancellationToken);
				return DomainHttpResultMapper.ToResult(result);
			})
			.RequireAuthorization(TicketingAuthPolicies.WorkTicket)
			.WithName("GetTeam");

		teams.MapPut("/{teamId}/members/{userOid}", async (
				string teamId,
				string userOid,
				SaveTeamMemberHttpRequest request,
				ITeamManagementService teamManagement,
				CancellationToken cancellationToken) =>
			{
				var result = await teamManagement.SaveMemberAsync(
					new SaveTeamMemberCommand
					{
						TeamId = teamId,
						UserOid = userOid,
						Role = request.Role,
						IsActive = request.IsActive
					},
					cancellationToken);

				return DomainHttpResultMapper.ToResult(result);
			})
			.RequireAuthorization(TicketingAuthPolicies.ManageTeams)
			.WithName("SaveTeamMember");

		teams.MapGet("/{teamId}/members", async (
				string teamId,
				bool includeInactive,
				int? pageSize,
				string? pageToken,
				ITeamManagementService teamManagement,
				CancellationToken cancellationToken) =>
			{
				var result = await teamManagement.GetMembersAsync(teamId, includeInactive, pageSize, pageToken, cancellationToken);
				return DomainHttpResultMapper.ToResult(result);
			})
			.RequireAuthorization(TicketingAuthPolicies.WorkTicket)
			.WithName("GetTeamMembers");

		teams.MapPost("/{teamId}/category-assignments", async (
				string teamId,
				SaveTeamCategoryAssignmentHttpRequest request,
				ITeamManagementService teamManagement,
				CancellationToken cancellationToken) =>
			{
				var result = await teamManagement.SaveCategoryAssignmentAsync(
					new SaveTeamCategoryAssignmentCommand
					{
						AssignmentId = request.AssignmentId,
						TeamId = teamId,
						TypeId = request.TypeId,
						CategoryId = request.CategoryId,
						SubcategoryId = request.SubcategoryId,
						Priority = request.Priority,
						IsDefault = request.IsDefault,
						IsActive = request.IsActive,
						SortOrder = request.SortOrder
					},
					cancellationToken);

				return DomainHttpResultMapper.ToCreated(
					result,
					assignment => $"/api/teams/{assignment.TeamId}/category-assignments/{assignment.AssignmentId}");
			})
			.RequireAuthorization(TicketingAuthPolicies.ManageTeams)
			.WithName("SaveTeamCategoryAssignment");

		teams.MapGet("/category-assignments", async (
				string? teamId,
				bool includeInactive,
				int? pageSize,
				string? pageToken,
				ITeamManagementService teamManagement,
				CancellationToken cancellationToken) =>
			{
				var result = await teamManagement.GetCategoryAssignmentsAsync(teamId, includeInactive, pageSize, pageToken, cancellationToken);
				return DomainHttpResultMapper.ToResult(result);
			})
			.RequireAuthorization(TicketingAuthPolicies.ManageTeams)
			.WithName("GetTeamCategoryAssignments");

		return teams;
	}
}
