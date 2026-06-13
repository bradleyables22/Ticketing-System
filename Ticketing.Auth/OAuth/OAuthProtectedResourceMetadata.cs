using System.Text.Json.Serialization;

namespace Ticketing.Auth.OAuth;

internal sealed record OAuthProtectedResourceMetadata
{
	[JsonPropertyName("resource")]
	public required string Resource { get; init; }

	[JsonPropertyName("authorization_servers")]
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public IReadOnlyList<string>? AuthorizationServers { get; init; }

	[JsonPropertyName("bearer_methods_supported")]
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public IReadOnlyList<string>? BearerMethodsSupported { get; init; }

	[JsonPropertyName("scopes_supported")]
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public IReadOnlyList<string>? ScopesSupported { get; init; }

	[JsonPropertyName("resource_name")]
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public string? ResourceName { get; init; }

	[JsonPropertyName("resource_documentation")]
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public string? ResourceDocumentation { get; init; }

	[JsonPropertyName("resource_policy_uri")]
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public string? ResourcePolicyUri { get; init; }

	[JsonPropertyName("resource_tos_uri")]
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public string? ResourceTermsOfServiceUri { get; init; }
}
