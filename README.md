# Ticketing System

A self-hosted IT ticketing system intended to replace an existing Issuetrak deployment.

The system is being designed around Microsoft-native infrastructure:

- Azure AD / Microsoft Entra ID for identity
- Azure Storage Account services for persistence and background work
- ASP.NET Core for the server host
- Minimal APIs for REST endpoints
- MCP tools for AI clients

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

Ticketing.Mcp
  Model Context Protocol tool layer over the domain services.
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
- submitter/requester
- creator when a worker submits on behalf of someone else
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

Public domain workflow and management services return `DomainResult<T>` so REST, MCP, and future UI adapters can map success/failure explicitly without catching business-rule exceptions. Permission evaluation is kept as an internal domain collaborator.

The domain layer owns rules such as:

- normal callers cannot choose their own submitter id
- technicians, managers, and admins can create tickets on behalf of another user while preserving who created the ticket
- users can view their own tickets
- technicians can work tickets for teams they belong to
- internal notes require worker access
- audit history is visible only to ticket workers
- managers and admins manage teams and taxonomy

## Server Configuration

Outside the Development environment, the server expects this storage environment variable:

```text
TICKETING_AZURE_STORAGE_CONNECTION_STRING
```

Supported configuration fallbacks:

```text
ConnectionStrings__AzureStorage
```

The Development environment defaults to `UseDevelopmentStorage=true` for Azurite.

Auth configuration is documented in [Ticketing.Auth/README.md](Ticketing.Auth/README.md). MCP setup and tool behavior are documented in [Ticketing.Mcp/README.md](Ticketing.Mcp/README.md).

Current server composition:

```csharp
builder.Services.AddTicketingAuth(tenantId, clientId);
builder.Services.AddTicketingData(azureStorageConnectionString);
builder.Services.AddTicketingGraphUserDirectory(...);
builder.Services.AddTicketingDomain();
builder.Services.AddTicketingRest();
builder.Services.AddTicketingMcp();
builder.Services.AddOpenApi();
```

In Development, `TicketingAuth:Mode=Development` swaps Entra JWT validation for local development auth.

REST endpoints are mapped from `Ticketing.Rest`:

```csharp
app.MapOpenApi();
app.MapScalarApiReference("/api/docs");
app.MapTicketingMcp();
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

## Local Development

The Development environment is configured to run without Entra ID or an Azure Storage account.

Local development uses:

- development auth from `Ticketing.Auth`
- auto-started Azurite for local Azure Table, Blob, and Queue Storage
- `UseDevelopmentStorage=true` from `Ticketing.Server/appsettings.Development.json`

Run the server:

```powershell
dotnet run --project .\Ticketing.Server
```

In Development mode, the server checks the local Azurite ports and starts Azurite automatically when `UseDevelopmentStorage=true`.

If Azurite is not installed globally, the server falls back to `npx.cmd -y azurite`. Node.js must be installed.

To install Azurite globally:

```powershell
npm.cmd install -g azurite
```

Manual Azurite startup still works:

```powershell
azurite.cmd --location .\.azurite --skipApiVersionCheck
```

Or start Azurite and the API together with the helper script:

```powershell
.\scripts\start-local.ps1
```

Automatic Azurite startup can be disabled:

```json
{
  "Ticketing": {
    "LocalDevelopment": {
      "Azurite": {
        "Enabled": false
      }
    }
  }
}
```

The launch profile sets:

```text
ASPNETCORE_ENVIRONMENT=Development
```

Development auth creates a local admin user by default, so Scalar can call protected endpoints immediately at:

```text
https://localhost:7173/api/docs
```

You can override the local user for a request with development-only headers such as:

```text
X-Ticketing-Dev-User-Oid
X-Ticketing-Dev-Roles
X-Ticketing-Dev-Scopes
```

Development auth refuses to run outside the Development environment. Full auth behavior and local-user configuration are documented in [Ticketing.Auth/README.md](Ticketing.Auth/README.md).

## REST API

`Ticketing.Rest` is a thin Minimal API adapter. It should not own business rules. Endpoint handlers call the domain services and translate `DomainResult` responses into HTTP results.

Current route groups:

- `/api/me`
- `/api/tickets`
- `/api/teams`
- `/api/taxonomy`
- `/api/users`
- `/api/system`

Ticket endpoints currently cover create, read, search, queues, notes, attachments, audit, assignment, team reassignment, status transitions, close, reopen, and cancel workflows.

Ticket creation defaults to the authenticated user as the submitter/requester. Technicians, managers, and admins can pass `submitterOid` on create to submit on behalf of another user. The ticket stores both `SubmitterOid` and `CreatedByOid`, so requester history, notifications, reporting, and audit can distinguish the employee needing help from the worker who entered the ticket. On-behalf submitters must exist in the local user profile cache or be resolvable through Microsoft Graph when Graph user search is configured.

Team endpoints currently cover team maintenance, membership maintenance, and category routing assignments.

Taxonomy endpoints currently cover types, categories, and subcategories.

User endpoints currently expose the authenticated user's profile, roles, permissions, and team memberships. User search can use Microsoft Graph when configured and falls back to the local `UserProfiles` cache in development/offline mode.

System endpoints currently expose admin-only system info, and `/health` exposes the ASP.NET Core health endpoint.

REST auth requirements are documented in [Ticketing.Auth/README.md](Ticketing.Auth/README.md).

List-style REST endpoints accept `pageSize` and `pageToken`. Missing page sizes default to 50 and values above 500 are clamped to 500. List responses use this envelope:

```json
{
  "items": [],
  "nextPageToken": null
}
```

Pass `nextPageToken` back as `pageToken` to read the next page.

## MCP API

`Ticketing.Mcp` exposes the same domain workflows as authenticated MCP tools at `/mcp` by default. It uses the official ASP.NET Core MCP transport package, the same auth stack as REST, and structured `DomainResult`-style envelopes for tool responses.

MCP setup, configuration, auth policy mapping, tool inventory, local smoke checks, and attachment limitations are documented in [Ticketing.Mcp/README.md](Ticketing.Mcp/README.md).

## Attachment Uploads

Ticket attachments are image-only by default. Uploads are accepted as `multipart/form-data` on:

```text
POST /api/tickets/{ticketId}/attachments
```

Default upload policy:

- maximum image size: `10 MiB`
- supported content types: `image/jpeg`, `image/png`, `image/gif`, `image/webp`, `image/bmp`, `image/tiff`, `image/heic`, `image/heif`, `image/avif`
- supported extensions: `.jpg`, `.jpeg`, `.png`, `.gif`, `.webp`, `.bmp`, `.tif`, `.tiff`, `.heic`, `.heif`, `.avif`
- file signatures are validated before the image is stored
- SVG is not allowed by default

Oversized uploads return `413 Payload Too Large`. Invalid file types, mismatched extensions, or mismatched content types return validation errors.

Environment configuration:

```text
TICKETING_ATTACHMENTS_MAX_SIZE_MB=10
TICKETING_ATTACHMENTS_MAX_SIZE_BYTES=10485760
TICKETING_ATTACHMENTS_ALLOWED_CONTENT_TYPES=image/jpeg;image/png;image/webp
TICKETING_ATTACHMENTS_ALLOWED_EXTENSIONS=.jpg;.jpeg;.png;.webp
TICKETING_ATTACHMENTS_VALIDATE_IMAGE_SIGNATURES=true
```

Supported configuration keys:

```text
Ticketing__Attachments__MaxSizeMegabytes
Ticketing__Attachments__MaxSizeBytes
Ticketing__Attachments__AllowedContentTypes
Ticketing__Attachments__AllowedExtensions
Ticketing__Attachments__ValidateImageSignatures
```

`MaxSizeBytes` wins over `MaxSizeMegabytes` when both are configured. The server also applies the same limit to multipart body handling with a small allowance for multipart overhead.

## Email Notification Queue

The API does not send email directly. Ticket workflows enqueue email notification work to Azure Queue Storage and leave actual sending to a host-provided worker, Azure Function, Logic App, or other consumer.

Default queue name:

```text
ticket-email-notifications
```

The queue is created by the storage initializer alongside the existing tables, blob container, and work queue.

Default behavior publishes notification messages for:

- ticket created
- ticket updated
- ticket assigned
- ticket team assigned
- ticket status changed, resolved, or cancelled
- ticket closed
- ticket reopened
- public note added
- internal note added
- attachment added
- attachment deleted

Notification enqueueing is best-effort. If a ticket mutation succeeds but queue publishing fails, the ticket change is kept and the API logs a warning instead of rolling the workflow back.

Queue messages are JSON with camel-case property names:

```json
{
  "schemaVersion": 1,
  "notificationId": "019...",
  "workType": "ticket-email-notification",
  "eventName": "ticket.created",
  "templateKey": "ticket.created",
  "createdUtc": "2026-06-14T18:00:00+00:00",
  "ticket": {
    "ticketId": "019...",
    "ticketNumber": "TCK-000001",
    "title": "Printer is jammed",
    "description": null,
    "status": "Open",
    "priority": "Normal",
    "submitterOid": "user-1",
    "createdByOid": "user-1",
    "assigneeOid": null,
    "assignedTeamId": "helpdesk"
  },
  "actor": {
    "userOid": "user-1",
    "displayName": "Alex User",
    "email": "alex@example.com"
  },
  "recipients": [
    {
      "userOid": "user-2",
      "displayName": "Taylor Tech",
      "email": "taylor@example.com",
      "roles": ["teamMember"]
    }
  ],
  "data": {}
}
```

The sender should treat `eventName` or `templateKey` as the template selector. Recipient emails are included when they exist in the local `UserProfiles` cache; consumers can also resolve recipients by `userOid`.

Environment configuration:

```text
TICKETING_EMAIL_NOTIFICATIONS_ENABLED=true
TICKETING_EMAIL_NOTIFICATIONS_QUEUE_NAME=ticket-email-notifications
TICKETING_EMAIL_NOTIFICATIONS_EXCLUDE_ACTOR=true
TICKETING_EMAIL_NOTIFICATIONS_MAX_TEAM_RECIPIENTS=50
TICKETING_EMAIL_NOTIFICATIONS_INCLUDE_TICKET_DESCRIPTION=false
```

Per-event switches:

```text
TICKETING_EMAIL_NOTIFICATIONS_EVENT_TICKET_CREATED=true
TICKETING_EMAIL_NOTIFICATIONS_EVENT_TICKET_UPDATED=true
TICKETING_EMAIL_NOTIFICATIONS_EVENT_TICKET_ASSIGNED=true
TICKETING_EMAIL_NOTIFICATIONS_EVENT_TEAM_ASSIGNED=true
TICKETING_EMAIL_NOTIFICATIONS_EVENT_STATUS_CHANGED=true
TICKETING_EMAIL_NOTIFICATIONS_EVENT_TICKET_CLOSED=true
TICKETING_EMAIL_NOTIFICATIONS_EVENT_TICKET_REOPENED=true
TICKETING_EMAIL_NOTIFICATIONS_EVENT_PUBLIC_NOTE_ADDED=true
TICKETING_EMAIL_NOTIFICATIONS_EVENT_INTERNAL_NOTE_ADDED=true
TICKETING_EMAIL_NOTIFICATIONS_EVENT_ATTACHMENT_ADDED=true
TICKETING_EMAIL_NOTIFICATIONS_EVENT_ATTACHMENT_DELETED=true
```

Supported configuration keys:

```text
Ticketing__EmailNotifications__Enabled
Ticketing__EmailNotifications__QueueName
Ticketing__EmailNotifications__ExcludeActorFromRecipients
Ticketing__EmailNotifications__MaxTeamRecipients
Ticketing__EmailNotifications__IncludeTicketDescription
Ticketing__EmailNotifications__Events__TicketCreated
Ticketing__EmailNotifications__Events__TicketUpdated
Ticketing__EmailNotifications__Events__TicketAssigned
Ticketing__EmailNotifications__Events__TeamAssigned
Ticketing__EmailNotifications__Events__StatusChanged
Ticketing__EmailNotifications__Events__TicketClosed
Ticketing__EmailNotifications__Events__TicketReopened
Ticketing__EmailNotifications__Events__PublicNoteAdded
Ticketing__EmailNotifications__Events__InternalNoteAdded
Ticketing__EmailNotifications__Events__AttachmentAdded
Ticketing__EmailNotifications__Events__AttachmentDeleted
```

## Microsoft Graph User Search

When configured, `/api/users` searches Microsoft Graph users with application permissions, then refreshes the local profile cache with returned users.

Required Microsoft Graph app permission:

```text
User.Read.All
```

Configuration:

```text
TICKETING_GRAPH_ENABLED=true
TICKETING_GRAPH_TENANT_ID=<tenant-id>
TICKETING_GRAPH_CLIENT_ID=<client-id>
TICKETING_GRAPH_CLIENT_SECRET=<client-secret>
```

Supported configuration keys:

```text
Ticketing__Graph__Enabled
Ticketing__Graph__TenantId
Ticketing__Graph__ClientId
Ticketing__Graph__ClientSecret
Ticketing__Graph__GraphBaseUri
```

If Graph is not enabled, user search uses the local profile cache.

## Build

Build the solution:

```powershell
dotnet build .\Ticketing-System.slnx
```

## Current Status

Implemented:

- Azure Storage-backed data layer
- one-time storage startup initializer
- Azurite-friendly local storage configuration
- Entra JWT authentication layer
- development auth for local runs without Entra
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
- Microsoft Graph-backed user search with local cache fallback
- ticket search endpoint backed by existing projection tables
- ticket status transition endpoints
- continuation-token response envelopes for REST list endpoints
- image-only attachment upload policy with configurable max size and signature validation
- email notification queue publishing for ticket workflow events
- authenticated MCP endpoint and ticketing tool surface
- admin system info and health endpoint
- teams, members, routing, taxonomy, notes, attachments, and audit foundations

Not yet implemented:

- UI
- projection repair worker
- email sending worker
- SLA and escalation rules
- full search/reporting strategy
- automated tests
- production observability and health checks

## Next Steps


1. Add initial integration tests against a development Azure Storage Account or Azurite where compatible.
2. Add background queue consumers for projection repair and notifications.
3. Add production observability around storage latency, authorization failures, and endpoint error rates.
4. Start the first UI or add MCP resources/prompts if clients need richer guided workflows.
