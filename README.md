# Ticketing System

A self-hosted IT ticketing system intended to replace an existing Issuetrak deployment.

The system is being designed around Microsoft-native infrastructure:

- Azure AD / Microsoft Entra ID for identity
- Azure Storage Account services for persistence and background work
- ASP.NET Core for the server host
- Minimal APIs for REST endpoints
- Future interaction surfaces such as MCP and UI clients

## Current Architecture

The solution is split into focused projects so REST, MCP, and future UI-facing surfaces can share the same business rules.

```text
Ticketing.Server
  ASP.NET Core host and composition root.

Ticketing.Auth
  Entra ID authentication, app roles, policies, and current-user access.

Ticketing.Data
  Azure Storage-backed persistence layer.

Ticketing.Domain
  Business logic and workflow rules shared by all interaction surfaces.

Ticketing.Rest
  Minimal API endpoint layer over the domain services.
```

The intended dependency direction is:

```text
REST / MCP / UI
  -> Ticketing.Domain
    -> Ticketing.Data
    -> Ticketing.Auth
```

`Ticketing.Data` is intentionally Azure Storage-native. The goal is not database portability. If an organization wants this system, it runs on Azure Storage.

## Identity And Authorization

Azure AD / Microsoft Entra ID is the identity provider. Auth setup, app roles, delegated scopes, OAuth discovery, host configuration, and runtime behavior are documented in [Ticketing.Auth/README.md](Ticketing.Auth/README.md).

## Data Platform

The data layer uses Azure Storage Account services:

- Azure Table Storage for tickets, users, teams, taxonomy, projections, notes, audit, and attachment metadata
- Azure Blob Storage for attachment content
- Azure Queue Storage for background work such as projection repair and notifications

The storage initializer runs once at application startup and creates required tables, blob containers, and queues.

This behavior is registered by `AddTicketingData(...)` and can be disabled:

```csharp
builder.Services.AddTicketingData(connectionString, options =>
{
    options.InitializeStorageOnStartup = false;
});
```

## Ticket Model

Tickets currently support:

- title and description
- submitter
- assigned technician
- assigned team
- status
- priority
- type, category, and subcategory
- tags
- notes
- internal notes
- attachments
- opened and closed timestamps
- audit history

A ticket can be team-owned before it is assigned to an individual technician.

```text
AssignedTeamId
AssigneeOid
```

## Teams And Routing

Teams are modeled inside the ticketing system, not as Azure AD app roles.

Team concepts:

- Team
- Team member
- Team member role: `Member` or `Lead`
- Category routing assignment

Routing rules can target:

- subcategory
- category
- type
- priority-specific variants
- default fallback

When a ticket is created, the system resolves a team in this order:

```text
subcategory
category
type
default
```

Managers and admins manage teams and routing rules.

## Domain Layer

`Ticketing.Domain` is the business logic layer. It protects the rest of the system from putting workflow rules into REST endpoints, MCP tools, or UI code.

Current domain services:

- `ITicketWorkflowService`
- `ITeamManagementService`
- `ITaxonomyManagementService`
- `ITicketUserService`
- `ITicketDashboardService`

Public domain workflow and management services return `DomainResult<T>` so REST, MCP, and future UI adapters can map success/failure explicitly without catching business-rule exceptions. Permission evaluation is kept as an internal domain collaborator.

The domain layer owns rules such as:

- callers cannot choose their own submitter id
- users can view their own tickets
- technicians can work tickets for teams they belong to
- internal notes require worker access
- audit history is visible only to ticket workers
- managers and admins manage teams and taxonomy

## Server Configuration

The server currently expects this storage environment variable:

```text
TICKETING_AZURE_STORAGE_CONNECTION_STRING
```

Supported configuration fallbacks:

```text
ConnectionStrings__AzureStorage
```

Auth configuration is documented in [Ticketing.Auth/README.md](Ticketing.Auth/README.md).

Current server composition:

```csharp
builder.Services.AddTicketingAuth(tenantId, clientId);
builder.Services.AddTicketingData(azureStorageConnectionString);
builder.Services.AddTicketingDomain();
builder.Services.AddTicketingRest();
builder.Services.AddOpenApi();
```

REST endpoints are mapped from `Ticketing.Rest`:

```csharp
app.MapOpenApi();
app.MapScalarApiReference("/api/docs");
app.MapTicketingOAuthDiscovery();
app.MapTicketingRestApi();
app.MapHealthChecks("/health");
```

The Scalar API reference is available at:

```text
/api/docs
```

The OpenAPI document is available at:

```text
/openapi/v1.json
```

## REST API

`Ticketing.Rest` is a thin Minimal API adapter. It should not own business rules. Endpoint handlers call the domain services and translate `DomainResult` responses into HTTP results.

Current route groups:

- `/api/me`
- `/api/tickets`
- `/api/teams`
- `/api/taxonomy`
- `/api/users`
- `/api/dashboard`
- `/api/system`

Ticket endpoints currently cover create, read, search, queues, notes, attachments, audit, assignment, team reassignment, status transitions, close, reopen, and cancel workflows.

Team endpoints currently cover team maintenance, membership maintenance, and category routing assignments.

Taxonomy endpoints currently cover types, categories, and subcategories.

User endpoints currently expose the authenticated user's profile, roles, permissions, and team memberships. User search currently reads from the local `UserProfiles` cache that is populated as authenticated users interact with the system. A future Microsoft Graph integration can expand this to live Entra user search.

Dashboard endpoints currently expose live summary counts from the ticket projection tables.

System endpoints currently expose admin-only system info, and `/health` exposes the ASP.NET Core health endpoint.

REST auth requirements are documented in [Ticketing.Auth/README.md](Ticketing.Auth/README.md).

List-style REST endpoints accept `pageSize`. Missing values default to 50 and values above 500 are clamped to 500. The current API returns bounded result sets; continuation-token response envelopes are still a future REST contract upgrade.

## Build

Build the solution:

```powershell
dotnet build .\Ticketing-System.slnx
```

## Current Status

Implemented:

- Azure Storage-backed data layer
- one-time storage startup initializer
- Entra JWT authentication layer
- app roles and policies
- delegated scope enforcement for client permissions
- OAuth protected resource metadata and bearer discovery challenges
- domain workflow and permission layer
- domain result wrappers for service responses
- REST minimal API endpoint layer
- bounded `pageSize` handling for REST list endpoints
- HTTP mapping for domain result responses
- Scalar API reference and OpenAPI document mapping
- OpenAPI bearer authentication metadata
- current-user context and cached user lookup
- ticket search endpoint backed by existing projection tables
- ticket status transition endpoints
- dashboard summary endpoint
- admin system info and health endpoint
- teams, members, routing, taxonomy, notes, attachments, and audit foundations

Not yet implemented:

- MCP surface
- UI
- Microsoft Graph-backed live user search
- projection repair worker
- notification worker
- SLA and escalation rules
- full search/reporting strategy
- continuation-token response envelopes for long REST lists
- automated tests
- production observability and health checks

## Next Steps


1. Add initial integration tests against a development Azure Storage Account or Azurite where compatible.
2. Add Microsoft Graph-backed user search for assignment workflows.
3. Add background queue consumers for projection repair and notifications.
4. Add production observability around storage latency, authorization failures, and endpoint error rates.
5. Start the first UI or MCP surface on top of the domain layer.
