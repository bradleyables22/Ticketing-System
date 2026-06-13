using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Ticketing.Auth.Configuration;

namespace Ticketing.Auth.OAuth;

internal sealed class TicketingOAuthDiscoveryService
{
	private readonly TicketingAuthOptions _options;

	public TicketingOAuthDiscoveryService(IOptions<TicketingAuthOptions> options)
	{
		_options = options.Value;
	}

	public bool IsEnabled => _options.OAuthDiscovery.Enabled;

	public bool ShouldAddChallenges =>
		_options.OAuthDiscovery.Enabled
		&& _options.OAuthDiscovery.AddResourceMetadataToChallenges;

	public OAuthProtectedResourceMetadata CreateMetadata(HttpRequest request)
	{
		var discovery = _options.OAuthDiscovery;
		return new OAuthProtectedResourceMetadata
		{
			Resource = GetResourceIdentifier(request),
			AuthorizationServers = ToNullableList(GetAuthorizationServers()),
			BearerMethodsSupported = ToNullableList(NormalizeValues(discovery.BearerMethodsSupported)),
			ScopesSupported = ToNullableList(GetScopesSupported()),
			ResourceName = Normalize(discovery.ResourceName),
			ResourceDocumentation = Normalize(discovery.ResourceDocumentationUri),
			ResourcePolicyUri = Normalize(discovery.ResourcePolicyUri),
			ResourceTermsOfServiceUri = Normalize(discovery.ResourceTermsOfServiceUri)
		};
	}

	public string CreateWwwAuthenticateChallenge(
		HttpRequest request,
		string? error = null,
		string? errorDescription = null,
		string? errorUri = null)
	{
		var parameters = new List<string>
		{
			FormatChallengeParameter("resource_metadata", GetResourceMetadataUri(request))
		};

		var challengeScopes = GetChallengeScopes();
		if (challengeScopes.Count > 0)
		{
			parameters.Add(FormatChallengeParameter("scope", string.Join(' ', challengeScopes)));
		}

		if (!string.IsNullOrWhiteSpace(error))
		{
			parameters.Add(FormatChallengeParameter("error", error));
		}

		if (!string.IsNullOrWhiteSpace(errorDescription))
		{
			parameters.Add(FormatChallengeParameter("error_description", errorDescription));
		}

		if (!string.IsNullOrWhiteSpace(errorUri))
		{
			parameters.Add(FormatChallengeParameter("error_uri", errorUri));
		}

		return $"Bearer {string.Join(", ", parameters)}";
	}

	private string GetResourceIdentifier(HttpRequest request)
	{
		var configuredResource = Normalize(_options.OAuthDiscovery.ResourceIdentifier);
		if (configuredResource is not null)
		{
			return configuredResource.TrimEnd('/');
		}

		var scheme = GetForwardedHeaderValue(request, "X-Forwarded-Proto") ?? request.Scheme;
		var host = GetForwardedHeaderValue(request, "X-Forwarded-Host") ?? request.Host.Value;
		var pathBase = GetForwardedHeaderValue(request, "X-Forwarded-Prefix") ?? request.PathBase.Value;

		return $"{scheme}://{host}{NormalizePathBase(pathBase)}".TrimEnd('/');
	}

	private string GetResourceMetadataUri(HttpRequest request)
	{
		var configuredMetadataUri = Normalize(_options.OAuthDiscovery.ResourceMetadataUri);
		if (configuredMetadataUri is not null)
		{
			return configuredMetadataUri;
		}

		var resource = GetResourceIdentifier(request);
		var wellKnownPath = NormalizeWellKnownPath(_options.OAuthDiscovery.WellKnownPath);

		if (!Uri.TryCreate(resource, UriKind.Absolute, out var resourceUri))
		{
			return $"{resource.TrimEnd('/')}{wellKnownPath}";
		}

		var resourcePath = resourceUri.AbsolutePath is "/" or ""
			? null
			: resourceUri.AbsolutePath.Trim('/');
		var metadataPath = resourcePath is null
			? wellKnownPath
			: $"{wellKnownPath.TrimEnd('/')}/{resourcePath}";

		var builder = new UriBuilder(resourceUri.Scheme, resourceUri.Host, resourceUri.IsDefaultPort ? -1 : resourceUri.Port)
		{
			Path = metadataPath.TrimStart('/')
		};

		return builder.Uri.ToString().TrimEnd('/');
	}

	private IReadOnlyList<string> GetAuthorizationServers() =>
		ToOrderedList(_options.OAuthDiscovery.AuthorizationServers, [_options.Authority]);

	private IReadOnlyList<string> GetChallengeScopes()
	{
		var configuredChallengeScopes = ToOrderedList(_options.OAuthDiscovery.ChallengeScopes);
		return configuredChallengeScopes.Count > 0
			? configuredChallengeScopes
			: GetDefaultRequestScopes();
	}

	private IReadOnlyList<string> GetScopesSupported()
	{
		var apiScopes = _options.Scopes.All
			.Select(ToResourceScope);
		var defaultScopes = GetDefaultRequestScopes();
		return ToOrderedList(_options.OAuthDiscovery.ScopesSupported, apiScopes.Concat(defaultScopes));
	}

	private IReadOnlyList<string> GetDefaultRequestScopes()
	{
		var discovery = _options.OAuthDiscovery;
		var defaultScopes = new List<string>();

		if (discovery.IncludeDefaultScope)
		{
			defaultScopes.Add($"{GetResourceApplicationIdUri().TrimEnd('/')}/.default");
		}

		if (discovery.IncludeOpenIdScope)
		{
			defaultScopes.Add("openid");
		}

		if (discovery.IncludeProfileScope)
		{
			defaultScopes.Add("profile");
		}

		if (discovery.IncludeEmailScope)
		{
			defaultScopes.Add("email");
		}

		if (discovery.IncludeOfflineAccessScope)
		{
			defaultScopes.Add("offline_access");
		}

		return ToOrderedList(defaultValues: defaultScopes);
	}

	private string GetResourceApplicationIdUri() =>
		Normalize(_options.OAuthDiscovery.ResourceApplicationIdUri)
		?? $"api://{_options.ClientId}";

	private string ToResourceScope(string scope)
	{
		var normalizedScope = Normalize(scope);
		if (normalizedScope is null)
		{
			return scope;
		}

		if (normalizedScope.Contains("://", StringComparison.Ordinal)
			|| normalizedScope is "openid" or "profile" or "email" or "offline_access")
		{
			return normalizedScope;
		}

		return $"{GetResourceApplicationIdUri().TrimEnd('/')}/{normalizedScope}";
	}

	private static IReadOnlyList<string> ToOrderedList(
		IEnumerable<string>? configuredValues = null,
		IEnumerable<string>? defaultValues = null)
	{
		var values = new List<string>();
		AddValues(values, defaultValues);
		AddValues(values, configuredValues);
		return values;
	}

	private static IEnumerable<string> NormalizeValues(IEnumerable<string>? values) =>
		values?.Select(Normalize).Where(value => value is not null).Select(value => value!) ?? [];

	private static IReadOnlyList<string>? ToNullableList(IEnumerable<string> values)
	{
		var list = values.ToArray();
		return list.Length == 0 ? null : list;
	}

	private static void AddValues(List<string> target, IEnumerable<string>? values)
	{
		foreach (var value in NormalizeValues(values))
		{
			if (!target.Contains(value, StringComparer.OrdinalIgnoreCase))
			{
				target.Add(value);
			}
		}
	}

	private static string? Normalize(string? value) =>
		string.IsNullOrWhiteSpace(value) ? null : value.Trim();

	private static string NormalizePathBase(string? pathBase)
	{
		if (string.IsNullOrWhiteSpace(pathBase) || pathBase is "/")
		{
			return string.Empty;
		}

		var normalized = pathBase.Trim();
		return normalized.StartsWith('/')
			? normalized.TrimEnd('/')
			: $"/{normalized.TrimEnd('/')}";
	}

	private static string NormalizeWellKnownPath(string? path)
	{
		if (string.IsNullOrWhiteSpace(path))
		{
			return "/.well-known/oauth-protected-resource";
		}

		var normalized = path.Trim();
		return normalized.StartsWith('/')
			? normalized
			: $"/{normalized}";
	}

	private static string? GetForwardedHeaderValue(HttpRequest request, string headerName)
	{
		if (!request.Headers.TryGetValue(headerName, out var values))
		{
			return null;
		}

		return values
			.ToString()
			.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
			.FirstOrDefault();
	}

	private static string FormatChallengeParameter(string name, string value) =>
		$"{name}=\"{EscapeChallengeValue(value)}\"";

	private static string EscapeChallengeValue(string value) =>
		value.Replace("\\", "\\\\", StringComparison.Ordinal)
			.Replace("\"", "\\\"", StringComparison.Ordinal);
}
