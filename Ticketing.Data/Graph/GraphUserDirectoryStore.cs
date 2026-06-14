using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Ticketing.Data.Configuration;
using Ticketing.Data.Models;
using Ticketing.Data.Stores;

namespace Ticketing.Data.Graph;

internal sealed class GraphUserDirectoryStore : IUserDirectoryStore
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	private readonly TicketingGraphUserDirectoryOptions _options;
	private readonly HttpClient _httpClient = new();
	private GraphToken? _currentToken;

	public GraphUserDirectoryStore(IOptions<TicketingGraphUserDirectoryOptions> options)
	{
		_options = options.Value;
	}

	public async Task<PagedResult<TicketUserProfile>> SearchUsersAsync(
		string? query,
		bool includeInactive = false,
		int? pageSize = null,
		string? pageToken = null,
		CancellationToken cancellationToken = default)
	{
		ValidateOptions();

		var token = DecodePageToken(pageToken);
		var requestUri = token?.NextLink ?? BuildUsersUri(query, includeInactive, AzureStorage.Internal.AzureTablePageLimits.NormalizeResultSize(pageSize));
		using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
		request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await GetAccessTokenAsync(cancellationToken));

		using var response = await _httpClient.SendAsync(request, cancellationToken);
		response.EnsureSuccessStatusCode();

		await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
		var graphResponse = await JsonSerializer.DeserializeAsync<GraphUsersResponse>(stream, JsonOptions, cancellationToken)
			?? new GraphUsersResponse();

		return new PagedResult<TicketUserProfile>
		{
			Items = graphResponse.Value
				.Select(ToProfile)
				.Where(profile => includeInactive || profile.IsActive)
				.ToArray(),
			NextPageToken = EncodePageToken(graphResponse.NextLink)
		};
	}

	private async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
	{
		if (_currentToken is { ExpiresUtc: var expiresUtc }
			&& expiresUtc > DateTimeOffset.UtcNow.AddMinutes(5))
		{
			return _currentToken.AccessToken;
		}

		var tokenEndpoint = $"https://login.microsoftonline.com/{Uri.EscapeDataString(_options.TenantId)}/oauth2/v2.0/token";
		using var content = new FormUrlEncodedContent(
		[
			new KeyValuePair<string, string>("client_id", _options.ClientId),
			new KeyValuePair<string, string>("client_secret", _options.ClientSecret),
			new KeyValuePair<string, string>("scope", "https://graph.microsoft.com/.default"),
			new KeyValuePair<string, string>("grant_type", "client_credentials")
		]);

		using var response = await _httpClient.PostAsync(tokenEndpoint, content, cancellationToken);
		response.EnsureSuccessStatusCode();

		await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
		var tokenResponse = await JsonSerializer.DeserializeAsync<GraphTokenResponse>(stream, JsonOptions, cancellationToken)
			?? throw new InvalidOperationException("Microsoft Graph token response was empty.");

		_currentToken = new GraphToken(
			tokenResponse.AccessToken,
			DateTimeOffset.UtcNow.AddSeconds(Math.Max(60, tokenResponse.ExpiresIn)));

		return _currentToken.AccessToken;
	}

	private string BuildUsersUri(string? query, bool includeInactive, int pageSize)
	{
		var builder = new StringBuilder(NormalizeBaseUri(_options.GraphBaseUri));
		builder.Append("/users?");
		AppendQueryParameter(builder, "$select", "id,displayName,mail,userPrincipalName,department,jobTitle,accountEnabled");
		AppendQueryParameter(builder, "$top", pageSize.ToString());

		var filters = new List<string>();
		if (!includeInactive)
		{
			filters.Add("accountEnabled eq true");
		}

		if (!string.IsNullOrWhiteSpace(query))
		{
			var escapedQuery = EscapeODataString(query.Trim());
			filters.Add(
				$"(startswith(displayName,'{escapedQuery}') or startswith(mail,'{escapedQuery}') or startswith(userPrincipalName,'{escapedQuery}'))");
		}

		if (filters.Count > 0)
		{
			AppendQueryParameter(builder, "$filter", string.Join(" and ", filters));
		}

		return builder.ToString();
	}

	private static void AppendQueryParameter(StringBuilder builder, string name, string value)
	{
		if (builder[^1] is not '?' and not '&')
		{
			builder.Append('&');
		}

		builder.Append(Uri.EscapeDataString(name));
		builder.Append('=');
		builder.Append(Uri.EscapeDataString(value));
	}

	private static TicketUserProfile ToProfile(GraphUser user)
	{
		var userPrincipalName = Normalize(user.UserPrincipalName);
		var email = Normalize(user.Mail) ?? userPrincipalName;
		return new TicketUserProfile
		{
			UserOid = Normalize(user.Id) ?? throw new InvalidOperationException("Microsoft Graph user is missing an id."),
			DisplayName = Normalize(user.DisplayName) ?? email ?? user.Id!,
			Email = email,
			Department = Normalize(user.Department),
			JobTitle = Normalize(user.JobTitle),
			IsActive = user.AccountEnabled ?? true,
			LastSeenUtc = DateTimeOffset.UtcNow
		};
	}

	private void ValidateOptions()
	{
		if (!_options.Enabled)
		{
			throw new InvalidOperationException("Microsoft Graph user directory search is not enabled.");
		}

		if (string.IsNullOrWhiteSpace(_options.TenantId)
			|| string.IsNullOrWhiteSpace(_options.ClientId)
			|| string.IsNullOrWhiteSpace(_options.ClientSecret))
		{
			throw new InvalidOperationException("Microsoft Graph user directory search requires tenant id, client id, and client secret.");
		}
	}

	private static string NormalizeBaseUri(string graphBaseUri) =>
		string.IsNullOrWhiteSpace(graphBaseUri)
			? "https://graph.microsoft.com/v1.0"
			: graphBaseUri.Trim().TrimEnd('/');

	private static string? Normalize(string? value) =>
		string.IsNullOrWhiteSpace(value) ? null : value.Trim();

	private static string EscapeODataString(string value) =>
		value.Replace("'", "''", StringComparison.Ordinal);

	private static GraphUsersPageToken? DecodePageToken(string? pageToken)
	{
		if (string.IsNullOrWhiteSpace(pageToken))
		{
			return null;
		}

		try
		{
			var bytes = Convert.FromBase64String(pageToken);
			return JsonSerializer.Deserialize<GraphUsersPageToken>(bytes, JsonOptions);
		}
		catch (Exception exception) when (exception is FormatException or JsonException)
		{
			throw new ArgumentException("Page token is invalid.", nameof(pageToken), exception);
		}
	}

	private static string? EncodePageToken(string? nextLink) =>
		string.IsNullOrWhiteSpace(nextLink)
			? null
			: Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new GraphUsersPageToken(nextLink), JsonOptions)));

	private sealed record GraphToken(string AccessToken, DateTimeOffset ExpiresUtc);

	private sealed record GraphUsersPageToken(string NextLink);

	private sealed record GraphTokenResponse
	{
		[JsonPropertyName("access_token")]
		public required string AccessToken { get; init; }

		[JsonPropertyName("expires_in")]
		public int ExpiresIn { get; init; }
	}

	private sealed record GraphUsersResponse
	{
		[JsonPropertyName("value")]
		public IReadOnlyList<GraphUser> Value { get; init; } = Array.Empty<GraphUser>();

		[JsonPropertyName("@odata.nextLink")]
		public string? NextLink { get; init; }
	}

	private sealed record GraphUser
	{
		public string? Id { get; init; }

		public string? DisplayName { get; init; }

		public string? Mail { get; init; }

		public string? UserPrincipalName { get; init; }

		public string? Department { get; init; }

		public string? JobTitle { get; init; }

		public bool? AccountEnabled { get; init; }
	}
}
