# Ticketing.Mcp

`Ticketing.Mcp` is the Model Context Protocol adapter for the ticketing system. It exposes the existing domain services as authenticated MCP tools so AI clients can create, search, route, and update tickets without duplicating REST endpoint logic.

The project is intentionally thin:

- tool methods call `Ticketing.Domain` services
- domain services keep workflow and record-level permission rules
- `Ticketing.Auth` policies keep delegated scope and role requirements consistent with REST
- the server host owns final configuration

## Package And Transport

The project uses the official C# MCP SDK package:

```xml
<PackageReference Include="ModelContextProtocol.AspNetCore" Version="1.4.0" />
```

`Ticketing.Server` registers HTTP MCP support with:

```csharp
builder.Services.AddTicketingMcp(options => ConfigureMcpOptions(options, builder.Configuration));
```

and maps the endpoint with:

```csharp
app.MapTicketingMcp();
```

The default MCP endpoint is:

```text
/mcp
```

The SDK handles the HTTP transport details for clients. Configure ChatGPT, Claude, MCP Inspector, or another HTTP MCP-capable client with the server base URL plus `/mcp`.

Local development launch URLs are:

```text
https://localhost:7173/mcp
http://localhost:5089/mcp
```

## Configuration

Default options:

```csharp
new TicketingMcpOptions
{
    EndpointPath = "/mcp",
    RequireAuthorization = true,
    AuthorizationPolicy = TicketingAuthPolicies.Read
}
```

Environment variables:

```text
TICKETING_MCP_ENDPOINT_PATH=/mcp
TICKETING_MCP_REQUIRE_AUTHORIZATION=true
TICKETING_MCP_AUTHORIZATION_POLICY=Ticketing.Scope.Read
```

Configuration keys:

```text
Ticketing__Mcp__EndpointPath
Ticketing__Mcp__RequireAuthorization
Ticketing__Mcp__AuthorizationPolicy
```

`RequireAuthorization=false` is intended only for isolated development experiments. Production MCP endpoints should stay protected.

## Auth Behavior

The MCP endpoint uses the same ASP.NET Core authentication setup as REST.

In production:

- clients send Microsoft Entra ID bearer tokens
- the server validates the token as a protected API/resource server
- OAuth protected-resource discovery and bearer challenges come from `Ticketing.Auth`
- token issuance and refresh token handling stay with the client's OAuth provider

In Development:

- development auth from `Ticketing.Auth` supplies a local admin user
- development-only headers can override user oid, roles, and scopes
- Azurite/local storage behavior is unchanged

Auth setup is documented in:

```text
Ticketing.Auth/README.md
```

## Policy Model

MCP does not bypass REST-style authorization.

The endpoint defaults to `Ticketing.Scope.Read`, which lets clients discover and call read-capable tools when they have any accepted ticketing API scope.

Each tool also checks the matching policy before it calls the domain service:

```text
Read tools                 Ticketing.Scope.Read
Ticket create              Ticketing.SubmitTicket
Ticket update/note/status  Ticketing.Scope.Write
Ticket assignment          Ticketing.Scope.Write + Ticketing.WorkTicket
Ticket audit               Ticketing.Scope.Read + Ticketing.WorkTicket
Team management            Ticketing.ManageTeams
Taxonomy management        Ticketing.ManageTaxonomy
Worker-only lookups        Ticketing.Scope.Read + Ticketing.WorkTicket
Global queues              Ticketing.ViewAllTickets
Team queues                Ticketing.ViewWorkQueues
```

After policy checks pass, domain services still enforce ticket-level, team-level, submitter, internal note, and audit visibility rules.

## Result Shape

Tools return a structured envelope instead of relying on opaque MCP exceptions for expected business failures.

Success:

```json
{
  "success": true,
  "value": {}
}
```

Failure:

```json
{
  "success": false,
  "error": {
    "code": "forbidden",
    "message": "The MCP tool call requires the 'Ticketing.Scope.Write' authorization policy.",
    "type": "Forbidden",
    "resourceName": null,
    "resourceId": null
  }
}
```

Common error codes include:

```text
authentication_required
invalid_principal
forbidden
not_found
validation_error
payload_too_large
conflict
unexpected_error
```

## Paging

List tools use the same paging contract as REST:

```json
{
  "items": [],
  "nextPageToken": null
}
```

Pass `nextPageToken` back as `pageToken` to continue. `pageSize` is optional and is bounded by the domain/data layer.

## Tool Inventory

### User Tools

```text
ticketing_get_current_user
ticketing_get_user
ticketing_search_users
```

### Ticket Tools

```text
ticketing_create_ticket
ticketing_search_tickets
ticketing_get_ticket
ticketing_get_ticket_by_number
ticketing_get_my_tickets
ticketing_get_assigned_to_me
ticketing_get_unassigned_tickets
ticketing_get_tickets_by_status
ticketing_get_team_queue
ticketing_get_category_queue
ticketing_get_tickets_by_tag
ticketing_update_ticket
ticketing_add_note
ticketing_get_notes
ticketing_get_attachments
ticketing_get_attachment_metadata
ticketing_get_audit
ticketing_assign_ticket
ticketing_assign_team
ticketing_set_status
ticketing_close_ticket
ticketing_reopen_ticket
```

### Team Tools

```text
ticketing_save_team
ticketing_get_team
ticketing_list_teams
ticketing_get_my_team_memberships
ticketing_save_team_member
ticketing_list_team_members
ticketing_save_team_routing
ticketing_list_team_routing
```

### Taxonomy Tools

```text
ticketing_save_ticket_type
ticketing_list_ticket_types
ticketing_save_ticket_category
ticketing_list_ticket_categories
ticketing_save_ticket_subcategory
ticketing_list_ticket_subcategories
```

## Ticket Creation On Behalf Of A User

`ticketing_create_ticket` accepts `submitterOid`.

- omit `submitterOid` to create as the authenticated user
- pass `submitterOid` to create on behalf of another user
- on-behalf creation requires technician, manager, or admin permission
- the submitter must exist in the local profile cache or be resolvable through Graph when Graph user search is configured
- tickets store both `SubmitterOid` and `CreatedByOid`

## Attachments

MCP tools currently expose attachment metadata only:

```text
ticketing_get_attachments
ticketing_get_attachment_metadata
```

Binary image upload and download remain REST responsibilities:

```text
POST /api/tickets/{ticketId}/attachments
GET  /api/tickets/{ticketId}/attachments/{attachmentId}/content
```

This keeps file transport predictable across MCP clients.

## Local Smoke Check

Run the server:

```powershell
dotnet run --project .\Ticketing.Server
```

Then connect an MCP client to:

```text
https://localhost:7173/mcp
```

With development auth enabled, the client should discover 39 tools.
