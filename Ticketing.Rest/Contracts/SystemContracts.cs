namespace Ticketing.Rest.Contracts;

public sealed record SystemInfoResponse
{
	public required string ApplicationName { get; init; }

	public required string EnvironmentName { get; init; }

	public string? Version { get; init; }

	public DateTimeOffset ServerTimeUtc { get; init; }
}
