using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Ticketing.Domain.Models;
using Ticketing.Mcp.Contracts;

namespace Ticketing.Mcp.Infrastructure;

public sealed class TicketingMcpAuthorizationService
{
	private readonly IAuthorizationService _authorization;
	private readonly IHttpContextAccessor _httpContextAccessor;

	public TicketingMcpAuthorizationService(
		IAuthorizationService authorization,
		IHttpContextAccessor httpContextAccessor)
	{
		_authorization = authorization;
		_httpContextAccessor = httpContextAccessor;
	}

	public async Task<TicketingMcpToolResult<T>> RunAsync<T>(
		string policyName,
		Func<CancellationToken, Task<DomainResult<T>>> operation,
		CancellationToken cancellationToken) =>
		await RunAsync([policyName], operation, cancellationToken);

	public async Task<TicketingMcpToolResult<T>> RunAsync<T>(
		IReadOnlyCollection<string> policyNames,
		Func<CancellationToken, Task<DomainResult<T>>> operation,
		CancellationToken cancellationToken)
	{
		foreach (var policyName in policyNames)
		{
			var authorization = await AuthorizeAsync(policyName, cancellationToken);
			if (authorization.IsFailure)
			{
				return TicketingMcpToolResults.From(DomainResult<T>.Failure(authorization.Error!));
			}
		}

		var result = await operation(cancellationToken);
		return TicketingMcpToolResults.From(result);
	}

	private async Task<DomainResult> AuthorizeAsync(string policyName, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		var user = _httpContextAccessor.HttpContext?.User;
		if (user?.Identity?.IsAuthenticated != true)
		{
			return DomainResult.Failure(
				DomainError.AuthenticationRequired("The MCP tool call requires an authenticated ticketing user."));
		}

		var result = await _authorization.AuthorizeAsync(user, policyName);
		return result.Succeeded
			? DomainResult.Success()
			: DomainResult.Failure(
				DomainError.Forbidden($"The MCP tool call requires the '{policyName}' authorization policy."));
	}
}
