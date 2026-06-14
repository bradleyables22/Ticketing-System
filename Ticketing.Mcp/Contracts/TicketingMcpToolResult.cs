using Ticketing.Domain.Models;

namespace Ticketing.Mcp.Contracts;

public sealed record TicketingMcpToolError
{
	public required string Code { get; init; }

	public required string Message { get; init; }

	public required string Type { get; init; }

	public string? ResourceName { get; init; }

	public string? ResourceId { get; init; }
}

public sealed record TicketingMcpToolResult
{
	public bool Success { get; init; }

	public TicketingMcpToolError? Error { get; init; }
}

public sealed record TicketingMcpToolResult<T>
{
	public bool Success { get; init; }

	public T? Value { get; init; }

	public TicketingMcpToolError? Error { get; init; }
}

internal static class TicketingMcpToolResults
{
	public static TicketingMcpToolResult From(DomainResult result) =>
		result.IsSuccess
			? new TicketingMcpToolResult { Success = true }
			: new TicketingMcpToolResult
			{
				Success = false,
				Error = ToToolError(result.Error!)
			};

	public static TicketingMcpToolResult<T> From<T>(DomainResult<T> result) =>
		result.IsSuccess
			? new TicketingMcpToolResult<T>
			{
				Success = true,
				Value = result.Value
			}
			: new TicketingMcpToolResult<T>
			{
				Success = false,
				Error = ToToolError(result.Error!)
			};

	private static TicketingMcpToolError ToToolError(DomainError error) =>
		new()
		{
			Code = error.Code,
			Message = error.Message,
			Type = error.Type.ToString(),
			ResourceName = error.ResourceName,
			ResourceId = error.ResourceId
		};
}
