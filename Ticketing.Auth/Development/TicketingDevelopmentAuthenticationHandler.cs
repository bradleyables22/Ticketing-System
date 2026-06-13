using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ticketing.Auth.Claims;
using Ticketing.Auth.Configuration;

namespace Ticketing.Auth.Development;

internal sealed class TicketingDevelopmentAuthenticationHandler : AuthenticationHandler<TicketingDevelopmentAuthOptions>
{
	public TicketingDevelopmentAuthenticationHandler(
		IOptionsMonitor<TicketingDevelopmentAuthOptions> options,
		ILoggerFactory logger,
		UrlEncoder encoder)
		: base(options, logger, encoder)
	{
	}

	protected override Task<AuthenticateResult> HandleAuthenticateAsync()
	{
		var options = Options;
		var userOid = GetConfiguredValue(options.UserOid, options.UserOidHeader);
		if (string.IsNullOrWhiteSpace(userOid))
		{
			return Task.FromResult(AuthenticateResult.Fail("Development auth requires a user object id."));
		}

		var tenantId = GetConfiguredValue(options.TenantId, options.TenantIdHeader);
		var displayName = GetConfiguredValue(options.DisplayName, options.DisplayNameHeader) ?? userOid;
		var email = GetConfiguredValue(options.Email, options.EmailHeader);
		var roles = GetConfiguredValues(options.Roles, options.RolesHeader);
		var scopes = GetConfiguredValues(options.Scopes, options.ScopesHeader);

		var claims = new List<Claim>
		{
			new(TicketingClaimTypes.ObjectId, userOid),
			new(TicketingClaimTypes.Name, displayName)
		};

		if (!string.IsNullOrWhiteSpace(tenantId))
		{
			claims.Add(new Claim(TicketingClaimTypes.TenantId, tenantId));
		}

		if (!string.IsNullOrWhiteSpace(email))
		{
			claims.Add(new Claim(TicketingClaimTypes.Email, email));
			claims.Add(new Claim(TicketingClaimTypes.PreferredUsername, email));
			claims.Add(new Claim(TicketingClaimTypes.Upn, email));
		}

		foreach (var role in roles)
		{
			claims.Add(new Claim(TicketingClaimTypes.Roles, role));
		}

		if (scopes.Count > 0)
		{
			claims.Add(new Claim(TicketingClaimTypes.Scopes, string.Join(' ', scopes)));
		}

		var identity = new ClaimsIdentity(
			claims,
			TicketingDevelopmentAuthenticationDefaults.AuthenticationScheme,
			TicketingClaimTypes.Name,
			TicketingClaimTypes.Roles);

		var principal = new ClaimsPrincipal(identity);
		var ticket = new AuthenticationTicket(principal, TicketingDevelopmentAuthenticationDefaults.AuthenticationScheme);
		return Task.FromResult(AuthenticateResult.Success(ticket));
	}

	private string? GetConfiguredValue(string? configuredValue, string headerName)
	{
		if (Options.AllowHeaderOverrides
			&& Request.Headers.TryGetValue(headerName, out var headerValues)
			&& !string.IsNullOrWhiteSpace(headerValues.FirstOrDefault()))
		{
			return headerValues.FirstOrDefault()!.Trim();
		}

		return string.IsNullOrWhiteSpace(configuredValue) ? null : configuredValue.Trim();
	}

	private IReadOnlyList<string> GetConfiguredValues(IReadOnlyCollection<string>? configuredValues, string headerName)
	{
		if (Options.AllowHeaderOverrides
			&& Request.Headers.TryGetValue(headerName, out var headerValues)
			&& !string.IsNullOrWhiteSpace(headerValues.FirstOrDefault()))
		{
			return Split(headerValues.FirstOrDefault()!).ToArray();
		}

		return configuredValues?
			.Where(value => !string.IsNullOrWhiteSpace(value))
			.Select(value => value.Trim())
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToArray()
			?? [];
	}

	private static IEnumerable<string> Split(string value) =>
		value.Split([';', ',', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
			.Distinct(StringComparer.OrdinalIgnoreCase);
}
