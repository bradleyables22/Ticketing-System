# Ticketing.Auth

`Ticketing.Auth` owns authentication, coarse authorization policies, current-user claim extraction, delegated scope enforcement, and OAuth protected-resource discovery for the ticketing system.

The project is intentionally Entra-native. The server is a protected API/resource server. It validates access tokens but does not sign users in, issue tokens, store refresh tokens, or act as an OAuth authorization server.

## How It Works

Authentication uses Microsoft Entra ID JWT bearer tokens.

At startup, `Ticketing.Server` calls:

```csharp
builder.Services.AddTicketingAuth(tenantId, clientId, options =>
{
    // host-specific role, scope, and OAuth discovery options
});
```

`AddTicketingAuth` configures:

- JWT bearer authentication.
- issuer validation against `https://login.microsoftonline.com/{tenantId}/v2.0`.
- audience validation against `{clientId}`, `api://{clientId}`, and any configured application ID URI or additional audiences.
- `roles` as the ASP.NET role claim.
- `scp` and `scope` as delegated OAuth scope claims.
- app policies for ticketing roles and client scopes.
- a scoped `ICurrentTicketingUserAccessor`.
- OAuth protected-resource metadata and bearer challenges.

The durable user key is the Entra object id claim:

```text
oid
```

Email and display name are treated as display snapshots only.

## Authorization Model

Authorization has three layers:

- delegated scopes (`scp`) describe what the calling client was granted for this API.
- app roles (`roles`) describe the signed-in user's broad ticketing role.
- domain permissions decide whether that user can access a specific ticket, team, note, attachment, or queue.

This prevents a narrowly scoped client from inheriting every operation the signed-in user could perform elsewhere. For example, a manager using a client with only `Ticket.Read` can read tickets the manager is otherwise allowed to read, but that client cannot mutate tickets or manage teams.

## Delegated API Scopes

Default scope names:

```text
Ticket.Read
Ticket.Write
Ticket.Manage
Ticket.System
```

Scope hierarchy:

```text
Ticket.System -> Ticket.Manage -> Ticket.Write -> Ticket.Read
```

Meaning:

```text
Ticket.Read
  Read current user context, tickets, ticket notes, attachments, teams, taxonomy, users, and dashboard summaries.

Ticket.Write
  Create and update tickets, add notes, upload/delete attachments, assign technicians, and change ticket status.

Ticket.Manage
  Manage teams, team membership, routing, taxonomy, and team reassignment.

Ticket.System
  Access admin-only system endpoints.
```

The API accepts either plain scope values such as `Ticket.Read` or resource-qualified scope values such as `api://{clientId}/Ticket.Read`.

Scope enforcement is enabled by default. Disable it only for local development or a temporary migration window:

```text
TicketingAuth__Scopes__RequireScopes=false
```

## App Roles

Default app role names:

```text
Ticket.Technician
Ticket.Manager
Ticket.Admin
```

Any authenticated tenant user can submit tickets when the calling client has `Ticket.Write`. App roles are for elevated IT access.

High-level role behavior:

- authenticated user: create tickets, view own tickets, add public notes to own tickets.
- technician: work tickets assigned to their teams or directly assigned to them.
- team lead: manage assignment within their team.
- manager: manage teams, taxonomy, routing, and broader queues.
- admin: full system access.

Team membership and team lead status are modeled in the ticketing data layer, not as Entra app roles.

## Policies

Current authorization policies:

```text
Ticketing.Scope.Read
Ticketing.Scope.Write
Ticketing.Scope.Manage
Ticketing.Scope.System
Ticketing.SubmitTicket
Ticketing.ViewWorkQueues
Ticketing.ViewAllTickets
Ticketing.WorkTicket
Ticketing.ManageTeams
Ticketing.ManageTaxonomy
Ticketing.Admin
```

Policy intent:

```text
Ticketing.Scope.Read
  Authenticated token with Ticket.Read, Ticket.Write, Ticket.Manage, or Ticket.System.

Ticketing.Scope.Write
  Authenticated token with Ticket.Write, Ticket.Manage, or Ticket.System.

Ticketing.Scope.Manage
  Authenticated token with Ticket.Manage or Ticket.System.

Ticketing.Scope.System
  Authenticated token with Ticket.System.

Ticketing.SubmitTicket
  Authenticated user plus write-capable client scope.

Ticketing.ViewWorkQueues
  Technician, manager, or admin user role plus read-capable client scope.

Ticketing.ViewAllTickets
  Manager or admin user role plus read-capable client scope.

Ticketing.WorkTicket
  Technician, manager, or admin user role.

Ticketing.ManageTeams
  Manager or admin user role plus manage-capable client scope.

Ticketing.ManageTaxonomy
  Manager or admin user role plus manage-capable client scope.

Ticketing.Admin
  Admin user role plus system client scope.
```

Ticket-specific checks still happen in `Ticketing.Domain`. Passing a route policy is not enough to view or mutate a ticket if the user is not the submitter, assignee, team member, team lead, manager, or admin required by that workflow.

## Entra App Registration Setup

Create or configure an Entra app registration for the ticketing API.

1. Set the application ID URI.

   Default expected value:

   ```text
   api://{clientId}
   ```

   If the value is different, configure the host with `AzureAd__ApplicationIdUri` or `TicketingAuth__OAuth__ResourceApplicationIdUri`.

2. Expose delegated API scopes.

   Add these scopes unless you override the names in host configuration:

   ```text
   Ticket.Read
   Ticket.Write
   Ticket.Manage
   Ticket.System
   ```

3. Add app roles.

   Add these roles unless you override the names in host configuration:

   ```text
   Ticket.Technician
   Ticket.Manager
   Ticket.Admin
   ```

4. Assign users or groups to app roles.

   Technicians, managers, and admins need the corresponding Entra app role assignment. Normal submitters do not need an app role.

5. Configure each client application.

   Clients should request delegated access to the scopes they need. Clients that request `{applicationIdUri}/.default` must already have the desired delegated scopes configured on their app registration.

6. Configure redirect URIs and token flow for each client.

   Browser/native clients usually use authorization code with PKCE. Clients that need refresh tokens should request `offline_access`. Refresh tokens are issued and refreshed by Entra; the ticketing API does not issue or store them.

## OAuth Discovery

OAuth protected-resource discovery is enabled by default.

Public routes:

```text
/.well-known/oauth-protected-resource
/.well-known/oauth-protected-resource/{resourcePath}
```

The metadata advertises:

- the protected resource identifier.
- supported authorization servers.
- supported bearer-token methods.
- ticketing scopes.
- optional documentation, policy, and terms links.

Example metadata shape:

```json
{
  "resource": "https://tickets.example.com",
  "authorization_servers": [
    "https://login.microsoftonline.com/{tenantId}/v2.0"
  ],
  "bearer_methods_supported": [
    "header"
  ],
  "scopes_supported": [
    "api://{clientId}/Ticket.Read",
    "api://{clientId}/Ticket.Write",
    "api://{clientId}/Ticket.Manage",
    "api://{clientId}/Ticket.System",
    "api://{clientId}/.default",
    "openid",
    "profile",
    "email",
    "offline_access"
  ],
  "resource_name": "Ticketing API"
}
```

Unauthenticated bearer challenges include `resource_metadata` so capable clients can discover the requirements:

```http
WWW-Authenticate: Bearer resource_metadata="https://tickets.example.com/.well-known/oauth-protected-resource", scope="api://{clientId}/.default openid profile email offline_access"
```

The default challenge requests:

```text
api://{clientId}/.default openid profile email offline_access
```

Hosters can override the challenge scopes if a client requires explicit API scopes instead of `.default`.

## Host Configuration

Required auth settings for Entra mode:

```text
TICKETING_AUTH_TENANT_ID
TICKETING_AUTH_CLIENT_ID
```

Supported fallbacks:

```text
AzureAd__TenantId
AzureAd__ClientId
```

Optional role settings:

```text
TICKETING_AUTH_ROLE_TECHNICIAN
TICKETING_AUTH_ROLE_MANAGER
TICKETING_AUTH_ROLE_ADMIN

TicketingAuth__Roles__Technician
TicketingAuth__Roles__Manager
TicketingAuth__Roles__Admin
```

Optional scope settings:

```text
TICKETING_AUTH_REQUIRE_SCOPES
TICKETING_AUTH_SCOPE_READ
TICKETING_AUTH_SCOPE_WRITE
TICKETING_AUTH_SCOPE_MANAGE
TICKETING_AUTH_SCOPE_SYSTEM

TicketingAuth__Scopes__RequireScopes
TicketingAuth__Scopes__Read
TicketingAuth__Scopes__Write
TicketingAuth__Scopes__Manage
TicketingAuth__Scopes__System
```

Optional OAuth discovery settings:

```text
TICKETING_AUTH_INSTANCE
TICKETING_AUTH_OAUTH_ENABLED
TICKETING_AUTH_OAUTH_RESOURCE_IDENTIFIER
TICKETING_AUTH_OAUTH_RESOURCE_METADATA_URI
TICKETING_AUTH_OAUTH_WELL_KNOWN_PATH
TICKETING_AUTH_OAUTH_RESOURCE_APPLICATION_ID_URI
TICKETING_AUTH_OAUTH_RESOURCE_NAME
TICKETING_AUTH_OAUTH_AUTHORIZATION_SERVERS
TICKETING_AUTH_OAUTH_SCOPES_SUPPORTED
TICKETING_AUTH_OAUTH_CHALLENGE_SCOPES
TICKETING_AUTH_OAUTH_BEARER_METHODS_SUPPORTED
TICKETING_AUTH_OAUTH_RESOURCE_DOCUMENTATION_URI
TICKETING_AUTH_OAUTH_RESOURCE_POLICY_URI
TICKETING_AUTH_OAUTH_RESOURCE_TOS_URI
TICKETING_AUTH_OAUTH_INCLUDE_DEFAULT_SCOPE
TICKETING_AUTH_OAUTH_INCLUDE_OPENID_SCOPE
TICKETING_AUTH_OAUTH_INCLUDE_PROFILE_SCOPE
TICKETING_AUTH_OAUTH_INCLUDE_EMAIL_SCOPE
TICKETING_AUTH_OAUTH_INCLUDE_OFFLINE_ACCESS_SCOPE

AzureAd__ApplicationIdUri
TicketingAuth__OAuth__Enabled
TicketingAuth__OAuth__ResourceIdentifier
TicketingAuth__OAuth__ResourceMetadataUri
TicketingAuth__OAuth__WellKnownPath
TicketingAuth__OAuth__ResourceApplicationIdUri
TicketingAuth__OAuth__ResourceName
TicketingAuth__OAuth__AuthorizationServers
TicketingAuth__OAuth__ScopesSupported
TicketingAuth__OAuth__ChallengeScopes
TicketingAuth__OAuth__BearerMethodsSupported
TicketingAuth__OAuth__ResourceDocumentationUri
TicketingAuth__OAuth__ResourcePolicyUri
TicketingAuth__OAuth__ResourceTermsOfServiceUri
TicketingAuth__OAuth__IncludeDefaultScope
TicketingAuth__OAuth__IncludeOpenIdScope
TicketingAuth__OAuth__IncludeProfileScope
TicketingAuth__OAuth__IncludeEmailScope
TicketingAuth__OAuth__IncludeOfflineAccessScope
```

Set `TicketingAuth__OAuth__ResourceIdentifier` or `TICKETING_AUTH_OAUTH_RESOURCE_IDENTIFIER` to the public HTTPS origin of the deployment when the app runs behind a reverse proxy. This value should be what external clients use as the API resource URL.

Set `TicketingAuth__OAuth__ResourceApplicationIdUri` or `AzureAd__ApplicationIdUri` when the Entra API application ID URI is not `api://{clientId}`.

List settings can be configured as arrays in structured config or as comma, semicolon, or space-separated environment values.

## Runtime Claim Mapping

The current user accessor reads:

```text
oid
tid
name
email
preferred_username
upn
roles
scp
scope
```

`oid` is required for domain operations. Missing or unauthenticated principals become domain errors that map to `401`.

## Development Auth

Development auth is available only when `ASPNETCORE_ENVIRONMENT=Development`.

Enable it with:

```text
TicketingAuth__Mode=Development
```

`Ticketing.Server/appsettings.Development.json` enables this by default.

In development mode, the API uses a local authentication handler instead of Entra JWT bearer validation. The handler creates an authenticated principal with normal ticketing claims:

```text
oid
tid
name
email
preferred_username
upn
roles
scp
```

Default local user:

```text
oid: local-admin
tid: local-tenant
name: Local Admin
email: local.admin@ticketing.test
roles: Ticket.Admin, Ticket.Manager, Ticket.Technician
scopes: Ticket.System, Ticket.Manage, Ticket.Write, Ticket.Read
```

This is intentionally not a production bypass. If `TicketingAuth__Mode=Development` is set outside the Development environment, startup fails.

Development auth settings:

```text
TICKETING_AUTH_MODE
TICKETING_AUTH_DEV_USER_OID
TICKETING_AUTH_DEV_TENANT_ID
TICKETING_AUTH_DEV_DISPLAY_NAME
TICKETING_AUTH_DEV_EMAIL
TICKETING_AUTH_DEV_ROLES
TICKETING_AUTH_DEV_SCOPES
TICKETING_AUTH_DEV_ALLOW_HEADER_OVERRIDES

TicketingAuth__Mode
TicketingAuth__Development__UserOid
TicketingAuth__Development__TenantId
TicketingAuth__Development__DisplayName
TicketingAuth__Development__Email
TicketingAuth__Development__Roles
TicketingAuth__Development__Scopes
TicketingAuth__Development__AllowHeaderOverrides
```

Header overrides are enabled by default in development mode. They are useful for testing user and permission cases from Scalar or curl:

```text
X-Ticketing-Dev-User-Oid
X-Ticketing-Dev-Tenant-Id
X-Ticketing-Dev-Display-Name
X-Ticketing-Dev-Email
X-Ticketing-Dev-Roles
X-Ticketing-Dev-Scopes
```

List values can be comma, semicolon, or space-separated.

## Public Endpoints Owned By Auth

```text
GET /.well-known/oauth-protected-resource
GET /.well-known/oauth-protected-resource/{resourcePath}
```

These endpoints are anonymous by design.

## Hoster Checklist

1. Register the API in Entra.
2. Expose the API application ID URI.
3. Add delegated scopes.
4. Add app roles.
5. Assign users or groups to elevated app roles.
6. Register each client app and grant only the scopes it needs.
7. Configure redirect URIs and PKCE/client-secret requirements on the client app.
8. Set `TICKETING_AUTH_TENANT_ID` and `TICKETING_AUTH_CLIENT_ID` on the server.
9. Set the public `ResourceIdentifier` when hosted behind a proxy or custom domain.
10. Verify `/.well-known/oauth-protected-resource` returns the expected public URLs and scopes.
