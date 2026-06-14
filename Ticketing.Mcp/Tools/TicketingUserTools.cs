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
public sealed class TicketingUserTools
{
	private readonly TicketingMcpAuthorizationService _authorization;
	private readonly ITicketUserService _users;

	public TicketingUserTools(
		TicketingMcpAuthorizationService authorization,
		ITicketUserService users)
	{
		_authorization = authorization;
		_users = users;
	}

	[McpServerTool(Name = "ticketing_get_current_user", ReadOnly = true)]
	[Description("Returns the authenticated user's ticketing context, including roles, scopes, permissions, and team memberships.")]
	public async Task<TicketingMcpToolResult<TicketingCurrentUserContext>> GetCurrentUserAsync(
		CancellationToken cancellationToken = default)
	{
		return await _authorization.RunAsync(
			TicketingAuthPolicies.Read,
			_users.GetCurrentAsync,
			cancellationToken);
	}

	[McpServerTool(Name = "ticketing_get_user", ReadOnly = true)]
	[Description("Returns a cached ticketing user profile by Microsoft Entra object id.")]
	public async Task<TicketingMcpToolResult<TicketUserProfile>> GetUserAsync(
		[Description("Microsoft Entra object id for the user.")] string userOid,
		CancellationToken cancellationToken = default)
	{
		return await _authorization.RunAsync(
			TicketingAuthPolicies.WorkTicket,
			ct => _users.GetUserAsync(userOid, ct),
			cancellationToken);
	}

	[McpServerTool(Name = "ticketing_search_users", ReadOnly = true)]
	[Description("Searches user profiles for assignment and administration workflows. Graph-backed hosts search Graph and refresh the local cache.")]
	public async Task<TicketingMcpToolResult<PagedResult<TicketUserProfile>>> SearchUsersAsync(
		[Description("Optional search text matched against display name, email, or user principal name.")] string? query = null,
		[Description("Include inactive cached profiles when true.")] bool includeInactive = false,
		[Description("Requested page size. Missing values default to the service default; oversized values are clamped by the service.")] int? pageSize = null,
		[Description("Continuation token from a previous response's nextPageToken.")] string? pageToken = null,
		CancellationToken cancellationToken = default)
	{
		return await _authorization.RunAsync(
			TicketingAuthPolicies.WorkTicket,
			ct => _users.SearchUsersAsync(query, includeInactive, pageSize, pageToken, ct),
			cancellationToken);
	}
}
