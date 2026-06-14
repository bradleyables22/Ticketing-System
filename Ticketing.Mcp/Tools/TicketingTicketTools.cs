using System.ComponentModel;
using ModelContextProtocol.Server;
using Ticketing.Auth;
using Ticketing.Data.Models;
using Ticketing.Domain.Models;
using Ticketing.Domain.Services;
using Ticketing.Mcp.Contracts;
using Ticketing.Mcp.Infrastructure;

namespace Ticketing.Mcp.Tools;

[McpServerToolType]
public sealed class TicketingTicketTools
{
	private readonly TicketingMcpAuthorizationService _authorization;
	private readonly ITicketWorkflowService _tickets;

	public TicketingTicketTools(
		TicketingMcpAuthorizationService authorization,
		ITicketWorkflowService tickets)
	{
		_authorization = authorization;
		_tickets = tickets;
	}

	[McpServerTool(Name = "ticketing_create_ticket")]
	[Description("Creates a ticket. By default the requester is the authenticated user; privileged workers can pass submitterOid to submit on behalf of someone else.")]
	public async Task<TicketingMcpToolResult<TicketRecord>> CreateTicketAsync(
		[Description("Short, user-facing ticket title.")] string title,
		[Description("Detailed description of the issue or request.")] string description,
		[Description("Optional Microsoft Entra object id for the requester when submitting on behalf of another user.")] string? submitterOid = null,
		[Description("Ticket priority.")] TicketPriority priority = TicketPriority.Normal,
		[Description("Optional ticket type id from taxonomy.")] string? typeId = null,
		[Description("Optional ticket category id from taxonomy.")] string? categoryId = null,
		[Description("Optional ticket subcategory id from taxonomy.")] string? subcategoryId = null,
		[Description("Optional normalized tags.")] string[]? tags = null,
		CancellationToken cancellationToken = default)
	{
		return await _authorization.RunAsync(
			TicketingAuthPolicies.SubmitTicket,
			ct => _tickets.CreateAsync(
				new CreateTicketCommand
				{
					Title = title,
					Description = description,
					SubmitterOid = submitterOid,
					Priority = priority,
					TypeId = typeId,
					CategoryId = categoryId,
					SubcategoryId = subcategoryId,
					Tags = NormalizeTags(tags)
				},
				ct),
			cancellationToken);
	}

	[McpServerTool(Name = "ticketing_search_tickets", ReadOnly = true)]
	[Description("Searches tickets with optional text, status, priority, participant, assignment, taxonomy, tag, and date filters.")]
	public async Task<TicketingMcpToolResult<PagedResult<TicketSummary>>> SearchTicketsAsync(
		[Description("Optional free-text query.")] string? query = null,
		[Description("Optional status filter.")] TicketStatus? status = null,
		[Description("Optional priority filter.")] TicketPriority? priority = null,
		[Description("Optional requester object id filter.")] string? submitterOid = null,
		[Description("Optional assignee object id filter.")] string? assigneeOid = null,
		[Description("Optional assigned team id filter.")] string? assignedTeamId = null,
		[Description("Optional ticket type id filter.")] string? typeId = null,
		[Description("Optional ticket category id filter.")] string? categoryId = null,
		[Description("Optional ticket subcategory id filter.")] string? subcategoryId = null,
		[Description("Optional tag filter.")] string? tag = null,
		[Description("Inclusive lower bound for opened timestamp in UTC.")] DateTimeOffset? openedFromUtc = null,
		[Description("Inclusive upper bound for opened timestamp in UTC.")] DateTimeOffset? openedToUtc = null,
		[Description("Inclusive lower bound for closed timestamp in UTC.")] DateTimeOffset? closedFromUtc = null,
		[Description("Inclusive upper bound for closed timestamp in UTC.")] DateTimeOffset? closedToUtc = null,
		[Description("Requested page size. Missing values default to the service default; oversized values are clamped by the service.")] int? pageSize = null,
		[Description("Continuation token from a previous response's nextPageToken.")] string? pageToken = null,
		CancellationToken cancellationToken = default)
	{
		return await _authorization.RunAsync(
			TicketingAuthPolicies.Read,
			ct => _tickets.SearchAsync(
				new TicketSearchCriteria
				{
					Query = query,
					Status = status,
					Priority = priority,
					SubmitterOid = submitterOid,
					AssigneeOid = assigneeOid,
					AssignedTeamId = assignedTeamId,
					TypeId = typeId,
					CategoryId = categoryId,
					SubcategoryId = subcategoryId,
					Tag = tag,
					OpenedFromUtc = openedFromUtc,
					OpenedToUtc = openedToUtc,
					ClosedFromUtc = closedFromUtc,
					ClosedToUtc = closedToUtc,
					PageSize = pageSize,
					PageToken = pageToken
				},
				ct),
			cancellationToken);
	}

	[McpServerTool(Name = "ticketing_get_ticket", ReadOnly = true)]
	[Description("Returns the full ticket record when the authenticated user can view it.")]
	public async Task<TicketingMcpToolResult<TicketRecord>> GetTicketAsync(
		[Description("Opaque ticket id returned by create/search/list operations.")] string ticketId,
		CancellationToken cancellationToken = default)
	{
		return await _authorization.RunAsync(
			TicketingAuthPolicies.Read,
			ct => _tickets.GetAsync(ticketId, ct),
			cancellationToken);
	}

	[McpServerTool(Name = "ticketing_get_ticket_by_number", ReadOnly = true)]
	[Description("Looks up a ticket by its human-readable ticket number.")]
	public async Task<TicketingMcpToolResult<TicketRecord>> GetTicketByNumberAsync(
		[Description("Human-readable ticket number, such as TCK-000001.")] string ticketNumber,
		CancellationToken cancellationToken = default)
	{
		return await _authorization.RunAsync(
			TicketingAuthPolicies.Read,
			ct => _tickets.GetByNumberAsync(ticketNumber, ct),
			cancellationToken);
	}

	[McpServerTool(Name = "ticketing_get_my_tickets", ReadOnly = true)]
	[Description("Lists tickets submitted by the authenticated user.")]
	public async Task<TicketingMcpToolResult<PagedResult<TicketSummary>>> GetMyTicketsAsync(
		[Description("Optional status filter.")] TicketStatus? status = null,
		[Description("Requested page size.")] int? pageSize = null,
		[Description("Continuation token from a previous response's nextPageToken.")] string? pageToken = null,
		CancellationToken cancellationToken = default)
	{
		return await _authorization.RunAsync(
			TicketingAuthPolicies.Read,
			ct => _tickets.GetMyTicketsAsync(status, pageSize, pageToken, ct),
			cancellationToken);
	}

	[McpServerTool(Name = "ticketing_get_assigned_to_me", ReadOnly = true)]
	[Description("Lists work queue tickets assigned to the authenticated worker.")]
	public async Task<TicketingMcpToolResult<PagedResult<TicketSummary>>> GetAssignedToMeAsync(
		[Description("Optional status filter.")] TicketStatus? status = null,
		[Description("Requested page size.")] int? pageSize = null,
		[Description("Continuation token from a previous response's nextPageToken.")] string? pageToken = null,
		CancellationToken cancellationToken = default)
	{
		return await _authorization.RunAsync(
			TicketingAuthPolicies.ViewWorkQueues,
			ct => _tickets.GetAssignedToMeAsync(status, pageSize, pageToken, ct),
			cancellationToken);
	}

	[McpServerTool(Name = "ticketing_get_unassigned_tickets", ReadOnly = true)]
	[Description("Lists global unassigned tickets for users allowed to view all tickets.")]
	public async Task<TicketingMcpToolResult<PagedResult<TicketSummary>>> GetUnassignedTicketsAsync(
		[Description("Optional status filter.")] TicketStatus? status = null,
		[Description("Requested page size.")] int? pageSize = null,
		[Description("Continuation token from a previous response's nextPageToken.")] string? pageToken = null,
		CancellationToken cancellationToken = default)
	{
		return await _authorization.RunAsync(
			TicketingAuthPolicies.ViewAllTickets,
			ct => _tickets.GetUnassignedAsync(status, pageSize, pageToken, ct),
			cancellationToken);
	}

	[McpServerTool(Name = "ticketing_get_tickets_by_status", ReadOnly = true)]
	[Description("Lists tickets by status for users allowed to view global status queues.")]
	public async Task<TicketingMcpToolResult<PagedResult<TicketSummary>>> GetTicketsByStatusAsync(
		[Description("Required status filter.")] TicketStatus status,
		[Description("Requested page size.")] int? pageSize = null,
		[Description("Continuation token from a previous response's nextPageToken.")] string? pageToken = null,
		CancellationToken cancellationToken = default)
	{
		return await _authorization.RunAsync(
			TicketingAuthPolicies.ViewAllTickets,
			ct => _tickets.GetByStatusAsync(status, pageSize, pageToken, ct),
			cancellationToken);
	}

	[McpServerTool(Name = "ticketing_get_team_queue", ReadOnly = true)]
	[Description("Lists tickets assigned to a specific team.")]
	public async Task<TicketingMcpToolResult<PagedResult<TicketSummary>>> GetTeamQueueAsync(
		[Description("Team id for the queue.")] string teamId,
		[Description("Optional status filter.")] TicketStatus? status = null,
		[Description("Requested page size.")] int? pageSize = null,
		[Description("Continuation token from a previous response's nextPageToken.")] string? pageToken = null,
		CancellationToken cancellationToken = default)
	{
		return await _authorization.RunAsync(
			TicketingAuthPolicies.ViewWorkQueues,
			ct => _tickets.GetTeamQueueAsync(teamId, status, pageSize, pageToken, ct),
			cancellationToken);
	}

	[McpServerTool(Name = "ticketing_get_category_queue", ReadOnly = true)]
	[Description("Lists queue items for a type, category, or subcategory. At least one taxonomy id should be supplied.")]
	public async Task<TicketingMcpToolResult<PagedResult<TicketSummary>>> GetCategoryQueueAsync(
		[Description("Optional ticket type id.")] string? typeId = null,
		[Description("Optional ticket category id.")] string? categoryId = null,
		[Description("Optional ticket subcategory id.")] string? subcategoryId = null,
		[Description("Optional status filter.")] TicketStatus? status = null,
		[Description("Requested page size.")] int? pageSize = null,
		[Description("Continuation token from a previous response's nextPageToken.")] string? pageToken = null,
		CancellationToken cancellationToken = default)
	{
		return await _authorization.RunAsync(
			TicketingAuthPolicies.Read,
			ct => _tickets.GetCategoryQueueAsync(
				typeId,
				categoryId,
				subcategoryId,
				status,
				pageSize,
				pageToken,
				ct),
			cancellationToken);
	}

	[McpServerTool(Name = "ticketing_get_tickets_by_tag", ReadOnly = true)]
	[Description("Lists tickets that have the requested tag.")]
	public async Task<TicketingMcpToolResult<PagedResult<TicketSummary>>> GetTicketsByTagAsync(
		[Description("Normalized tag value.")] string tag,
		[Description("Optional status filter.")] TicketStatus? status = null,
		[Description("Requested page size.")] int? pageSize = null,
		[Description("Continuation token from a previous response's nextPageToken.")] string? pageToken = null,
		CancellationToken cancellationToken = default)
	{
		return await _authorization.RunAsync(
			TicketingAuthPolicies.Read,
			ct => _tickets.GetByTagAsync(tag, status, pageSize, pageToken, ct),
			cancellationToken);
	}

	[McpServerTool(Name = "ticketing_update_ticket")]
	[Description("Updates editable ticket fields such as title, description, priority, taxonomy classification, and tags.")]
	public async Task<TicketingMcpToolResult<TicketRecord>> UpdateTicketAsync(
		[Description("Opaque ticket id returned by create/search/list operations.")] string ticketId,
		[Description("Optional ETag concurrency value from the ticket record.")] string? expectedETag = null,
		[Description("Optional replacement title.")] string? title = null,
		[Description("Optional replacement description.")] string? description = null,
		[Description("Optional replacement priority.")] TicketPriority? priority = null,
		[Description("Optional replacement type id.")] string? typeId = null,
		[Description("Optional replacement category id.")] string? categoryId = null,
		[Description("Optional replacement subcategory id.")] string? subcategoryId = null,
		[Description("When true, clears type/category/subcategory classification.")] bool clearClassification = false,
		[Description("Optional replacement tag list. Pass null to leave tags unchanged.")] string[]? tags = null,
		CancellationToken cancellationToken = default)
	{
		return await _authorization.RunAsync(
			TicketingAuthPolicies.Write,
			ct => _tickets.UpdateAsync(
				new UpdateTicketCommand
				{
					TicketId = ticketId,
					ExpectedETag = expectedETag,
					Title = title,
					Description = description,
					Priority = priority,
					TypeId = typeId,
					CategoryId = categoryId,
					SubcategoryId = subcategoryId,
					ClearClassification = clearClassification,
					Tags = tags is null ? null : NormalizeTags(tags)
				},
				ct),
			cancellationToken);
	}

	[McpServerTool(Name = "ticketing_add_note")]
	[Description("Adds a public or internal note to a ticket.")]
	public async Task<TicketingMcpToolResult<TicketNoteRecord>> AddNoteAsync(
		[Description("Opaque ticket id returned by create/search/list operations.")] string ticketId,
		[Description("Note body.")] string body,
		[Description("When true, marks the note internal to ticket workers.")] bool isInternal = false,
		CancellationToken cancellationToken = default)
	{
		return await _authorization.RunAsync(
			TicketingAuthPolicies.Write,
			ct => _tickets.AddNoteAsync(
				new AddTicketNoteCommand
				{
					TicketId = ticketId,
					Body = body,
					IsInternal = isInternal
				},
				ct),
			cancellationToken);
	}

	[McpServerTool(Name = "ticketing_get_notes", ReadOnly = true)]
	[Description("Lists notes for a ticket. Internal notes are included only when the authenticated user can work the ticket.")]
	public async Task<TicketingMcpToolResult<PagedResult<TicketNoteRecord>>> GetNotesAsync(
		[Description("Opaque ticket id returned by create/search/list operations.")] string ticketId,
		[Description("Requested page size.")] int? pageSize = null,
		[Description("Continuation token from a previous response's nextPageToken.")] string? pageToken = null,
		CancellationToken cancellationToken = default)
	{
		return await _authorization.RunAsync(
			TicketingAuthPolicies.Read,
			ct => _tickets.GetNotesAsync(ticketId, pageSize, pageToken, ct),
			cancellationToken);
	}

	[McpServerTool(Name = "ticketing_get_attachments", ReadOnly = true)]
	[Description("Lists non-deleted image attachment metadata for a ticket.")]
	public async Task<TicketingMcpToolResult<PagedResult<TicketAttachmentRecord>>> GetAttachmentsAsync(
		[Description("Opaque ticket id returned by create/search/list operations.")] string ticketId,
		[Description("Requested page size.")] int? pageSize = null,
		[Description("Continuation token from a previous response's nextPageToken.")] string? pageToken = null,
		CancellationToken cancellationToken = default)
	{
		return await _authorization.RunAsync(
			TicketingAuthPolicies.Read,
			ct => _tickets.GetAttachmentsAsync(ticketId, pageSize, pageToken, ct),
			cancellationToken);
	}

	[McpServerTool(Name = "ticketing_get_attachment_metadata", ReadOnly = true)]
	[Description("Gets metadata for a single ticket attachment. Use REST for binary download.")]
	public async Task<TicketingMcpToolResult<TicketAttachmentRecord>> GetAttachmentMetadataAsync(
		[Description("Opaque ticket id returned by create/search/list operations.")] string ticketId,
		[Description("Opaque attachment id returned by attachment-list operations.")] string attachmentId,
		CancellationToken cancellationToken = default)
	{
		return await _authorization.RunAsync(
			TicketingAuthPolicies.Read,
			ct => _tickets.GetAttachmentAsync(ticketId, attachmentId, ct),
			cancellationToken);
	}

	[McpServerTool(Name = "ticketing_get_audit", ReadOnly = true)]
	[Description("Lists audit history for a ticket. Audit history is restricted to users who can work the ticket.")]
	public async Task<TicketingMcpToolResult<PagedResult<TicketAuditEventRecord>>> GetAuditAsync(
		[Description("Opaque ticket id returned by create/search/list operations.")] string ticketId,
		[Description("Requested page size.")] int? pageSize = null,
		[Description("Continuation token from a previous response's nextPageToken.")] string? pageToken = null,
		CancellationToken cancellationToken = default)
	{
		return await _authorization.RunAsync(
			[TicketingAuthPolicies.Read, TicketingAuthPolicies.WorkTicket],
			ct => _tickets.GetAuditAsync(ticketId, pageSize, pageToken, ct),
			cancellationToken);
	}

	[McpServerTool(Name = "ticketing_assign_ticket")]
	[Description("Assigns or clears the individual assignee for a ticket.")]
	public async Task<TicketingMcpToolResult<TicketRecord>> AssignTicketAsync(
		[Description("Opaque ticket id returned by create/search/list operations.")] string ticketId,
		[Description("Microsoft Entra object id for the new assignee. Pass null to clear assignment.")] string? assigneeOid = null,
		[Description("Optional reason stored in audit/notification context.")] string? reason = null,
		[Description("Optional ETag concurrency value from the ticket record.")] string? expectedETag = null,
		CancellationToken cancellationToken = default)
	{
		return await _authorization.RunAsync(
			[TicketingAuthPolicies.Write, TicketingAuthPolicies.WorkTicket],
			ct => _tickets.AssignAsync(
				new AssignTicketCommand
				{
					TicketId = ticketId,
					AssigneeOid = assigneeOid,
					Reason = reason,
					ExpectedETag = expectedETag
				},
				ct),
			cancellationToken);
	}

	[McpServerTool(Name = "ticketing_assign_team")]
	[Description("Assigns or clears the owning team for a ticket.")]
	public async Task<TicketingMcpToolResult<TicketRecord>> AssignTeamAsync(
		[Description("Opaque ticket id returned by create/search/list operations.")] string ticketId,
		[Description("Team id for the new owning team. Pass null to clear team assignment.")] string? assignedTeamId = null,
		[Description("Optional reason stored in audit/notification context.")] string? reason = null,
		[Description("Optional ETag concurrency value from the ticket record.")] string? expectedETag = null,
		CancellationToken cancellationToken = default)
	{
		return await _authorization.RunAsync(
			TicketingAuthPolicies.ManageTeams,
			ct => _tickets.AssignTeamAsync(
				new AssignTicketTeamCommand
				{
					TicketId = ticketId,
					AssignedTeamId = assignedTeamId,
					Reason = reason,
					ExpectedETag = expectedETag
				},
				ct),
			cancellationToken);
	}

	[McpServerTool(Name = "ticketing_set_status")]
	[Description("Changes a ticket status to a non-closed state. Use ticketing_close_ticket for Closed so closure fields are captured consistently.")]
	public async Task<TicketingMcpToolResult<TicketRecord>> SetStatusAsync(
		[Description("Opaque ticket id returned by create/search/list operations.")] string ticketId,
		[Description("Target status.")] TicketStatus status,
		[Description("Optional reason stored in audit/notification context.")] string? reason = null,
		[Description("Optional ETag concurrency value from the ticket record.")] string? expectedETag = null,
		CancellationToken cancellationToken = default)
	{
		return await _authorization.RunAsync(
			TicketingAuthPolicies.Write,
			ct => _tickets.SetStatusAsync(
				new SetTicketStatusCommand
				{
					TicketId = ticketId,
					Status = status,
					Reason = reason,
					ExpectedETag = expectedETag
				},
				ct),
			cancellationToken);
	}

	[McpServerTool(Name = "ticketing_close_ticket")]
	[Description("Closes a ticket, records closure timestamp, and stores an optional resolution note.")]
	public async Task<TicketingMcpToolResult<TicketRecord>> CloseTicketAsync(
		[Description("Opaque ticket id returned by create/search/list operations.")] string ticketId,
		[Description("Optional resolution note stored in audit/notification context.")] string? resolutionNote = null,
		[Description("Optional ETag concurrency value from the ticket record.")] string? expectedETag = null,
		CancellationToken cancellationToken = default)
	{
		return await _authorization.RunAsync(
			[TicketingAuthPolicies.Write, TicketingAuthPolicies.WorkTicket],
			ct => _tickets.CloseAsync(
				new CloseTicketCommand
				{
					TicketId = ticketId,
					ResolutionNote = resolutionNote,
					ExpectedETag = expectedETag
				},
				ct),
			cancellationToken);
	}

	[McpServerTool(Name = "ticketing_reopen_ticket")]
	[Description("Reopens a ticket and clears the closure timestamp.")]
	public async Task<TicketingMcpToolResult<TicketRecord>> ReopenTicketAsync(
		[Description("Opaque ticket id returned by create/search/list operations.")] string ticketId,
		[Description("Optional reason stored in audit/notification context.")] string? reason = null,
		[Description("Optional ETag concurrency value from the ticket record.")] string? expectedETag = null,
		CancellationToken cancellationToken = default)
	{
		return await _authorization.RunAsync(
			TicketingAuthPolicies.Write,
			ct => _tickets.ReopenAsync(
				new ReopenTicketCommand
				{
					TicketId = ticketId,
					Reason = reason,
					ExpectedETag = expectedETag
				},
				ct),
			cancellationToken);
	}

	private static IReadOnlyCollection<string> NormalizeTags(string[]? tags) =>
		tags is null
			? Array.Empty<string>()
			: tags
				.Where(tag => !string.IsNullOrWhiteSpace(tag))
				.Select(tag => tag.Trim())
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.ToArray();
}
