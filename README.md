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
  Minimal API endpoint layer. Not built out yet.
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

Azure AD / Microsoft Entra ID is the identity provider.

The application uses the user's stable Entra object id, the `oid` claim, as the durable user key. Email and display name may be stored as snapshots for historical display, but they are not treated as permanent identifiers.

Current app roles:

```text
Ticket.Technician
Ticket.Manager
Ticket.Admin
```

Any authenticated tenant user can submit tickets. App roles are for elevated IT access.

High-level permissions:

- Authenticated user: create tickets, view own tickets, add public notes to own tickets.
- Technician: work tickets assigned to their teams or directly assigned to them.
- Team Lead: can manage assignment within their team.
- Manager: manage teams, taxonomy, routing, and broader queues.
- Admin: full system access.

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

Public domain workflow and management services return `DomainResult<T>` so REST, MCP, and future UI adapters can map success/failure explicitly without catching business-rule exceptions. Permission evaluation is kept as an internal domain collaborator.

The domain layer owns rules such as:

- callers cannot choose their own submitter id
- users can view their own tickets
- technicians can work tickets for teams they belong to
- internal notes require worker access
- audit history is visible only to ticket workers
- managers and admins manage teams and taxonomy

## Server Configuration

The server currently expects these environment variables:

```text
TICKETING_AUTH_TENANT_ID
TICKETING_AUTH_CLIENT_ID
TICKETING_AZURE_STORAGE_CONNECTION_STRING
```

Optional:

```text
TICKETING_AUTH_INSTANCE
```

Supported configuration fallbacks:

```text
AzureAd__TenantId
AzureAd__ClientId
ConnectionStrings__AzureStorage
```

Current server composition:

```csharp
builder.Services.AddTicketingAuth(tenantId, clientId);
builder.Services.AddTicketingData(azureStorageConnectionString);
builder.Services.AddTicketingDomain();
```

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
- domain workflow and permission layer
- domain result wrappers for service responses
- teams, members, routing, taxonomy, notes, attachments, and audit foundations

Not yet implemented:

- REST minimal API endpoints
- MCP surface
- UI
- projection repair worker
- notification worker
- SLA and escalation rules
- full search/reporting strategy
- automated tests
- production observability and health checks

## Next Steps


1. Build `Ticketing.Rest` as a thin minimal API adapter over `Ticketing.Domain`.
2. Add `DomainResult` to HTTP response mapping.
3. Add OpenAPI documentation.
4. Add initial integration tests against a development Azure Storage Account or Azurite where compatible.
5. Add background queue consumers for projection repair and notifications.
