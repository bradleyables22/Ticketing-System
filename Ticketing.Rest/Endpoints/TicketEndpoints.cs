using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Ticketing.Auth;
using Ticketing.Data.Models;
using Ticketing.Domain.Configuration;
using Ticketing.Domain.Models;
using Ticketing.Domain.Services;
using Ticketing.Rest.Contracts;
using Ticketing.Rest.Infrastructure;

namespace Ticketing.Rest.Endpoints;

internal static class TicketEndpoints
{
	public static RouteGroupBuilder MapTicketEndpoints(this RouteGroupBuilder api)
	{
		var tickets = api.MapGroup("/tickets")
			.WithTags("Tickets")
			.RequireAuthorization(TicketingAuthPolicies.Read);

		tickets.MapPost("/", async (
				CreateTicketHttpRequest request,
				ITicketWorkflowService ticketWorkflow,
				CancellationToken cancellationToken) =>
			{
				var result = await ticketWorkflow.CreateAsync(
					new CreateTicketCommand
					{
						Title = request.Title,
						Description = request.Description,
						SubmitterOid = request.SubmitterOid,
						Priority = request.Priority,
						TypeId = request.TypeId,
						CategoryId = request.CategoryId,
						SubcategoryId = request.SubcategoryId,
						Tags = request.Tags
					},
					cancellationToken);

				return DomainHttpResultMapper.ToCreated(result, ticket => $"/api/tickets/{ticket.TicketId}");
			})
			.RequireAuthorization(TicketingAuthPolicies.SubmitTicket)
			.WithName("CreateTicket")
			.WithCreatedDocs<TicketRecord>(
				"Create a ticket",
				"Creates a new ticket. By default the authenticated user is the submitter; technicians, managers, and admins can set submitterOid to create a ticket on behalf of another user. The server records both the requester and creator, resolves the initial team from taxonomy routing, writes audit history, updates queue projections, and enqueues email notification work.");

		tickets.MapGet("/search", async (
				string? q,
				TicketStatus? status,
				TicketPriority? priority,
				string? submitterOid,
				string? assigneeOid,
				string? assignedTeamId,
				string? typeId,
				string? categoryId,
				string? subcategoryId,
				string? tag,
				DateTimeOffset? openedFromUtc,
				DateTimeOffset? openedToUtc,
				DateTimeOffset? closedFromUtc,
				DateTimeOffset? closedToUtc,
				int? pageSize,
				string? pageToken,
				ITicketWorkflowService ticketWorkflow,
				CancellationToken cancellationToken) =>
			{
				var result = await ticketWorkflow.SearchAsync(
					new TicketSearchCriteria
					{
						Query = q,
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
					cancellationToken);

				return DomainHttpResultMapper.ToResult(result);
			})
			.WithName("SearchTickets")
			.WithOkDocs<PagedResult<TicketSummary>>(
				"Search tickets",
				"Searches tickets using optional text, status, priority, participant, assignment, taxonomy, tag, and date filters. Results are permission-filtered and returned as a paged envelope; pass nextPageToken back as pageToken to continue.");

		tickets.MapGet("/{ticketId}", async (
				string ticketId,
				ITicketWorkflowService ticketWorkflow,
				CancellationToken cancellationToken) =>
			{
				var result = await ticketWorkflow.GetAsync(ticketId, cancellationToken);
				return DomainHttpResultMapper.ToResult(result);
			})
			.WithName("GetTicket")
			.WithOkDocs<TicketRecord>(
				"Get a ticket",
				"Returns the full ticket record when the authenticated user can view it. Submitters can view their own tickets; workers can view tickets according to their role and team permissions.",
				notFound: true);

		tickets.MapGet("/by-number/{ticketNumber}", async (
				string ticketNumber,
				ITicketWorkflowService ticketWorkflow,
				CancellationToken cancellationToken) =>
			{
				var result = await ticketWorkflow.GetByNumberAsync(ticketNumber, cancellationToken);
				return DomainHttpResultMapper.ToResult(result);
			})
			.WithName("GetTicketByNumber")
			.WithOkDocs<TicketRecord>(
				"Get a ticket by number",
				"Looks up a ticket by its human-readable ticket number, then applies the same visibility rules as ticket-id lookup.",
				notFound: true);

		tickets.MapGet("/mine", async (
				TicketStatus? status,
				int? pageSize,
				string? pageToken,
				ITicketWorkflowService ticketWorkflow,
				CancellationToken cancellationToken) =>
			{
				var result = await ticketWorkflow.GetMyTicketsAsync(status, pageSize, pageToken, cancellationToken);
				return DomainHttpResultMapper.ToResult(result);
			})
			.WithName("GetMyTickets")
			.WithOkDocs<PagedResult<TicketSummary>>(
				"List my submitted tickets",
				"Returns tickets submitted by the authenticated user. Optionally filter by status and page through results with pageSize and pageToken.");

		tickets.MapGet("/assigned-to-me", async (
				TicketStatus? status,
				int? pageSize,
				string? pageToken,
				ITicketWorkflowService ticketWorkflow,
				CancellationToken cancellationToken) =>
			{
				var result = await ticketWorkflow.GetAssignedToMeAsync(status, pageSize, pageToken, cancellationToken);
				return DomainHttpResultMapper.ToResult(result);
			})
			.RequireAuthorization(TicketingAuthPolicies.ViewWorkQueues)
			.WithName("GetAssignedToMe")
			.WithOkDocs<PagedResult<TicketSummary>>(
				"List tickets assigned to me",
				"Returns work queue items assigned to the authenticated technician, manager, or admin. Optionally filter by status and page through results with pageSize and pageToken.");

		tickets.MapGet("/unassigned", async (
				TicketStatus? status,
				int? pageSize,
				string? pageToken,
				ITicketWorkflowService ticketWorkflow,
				CancellationToken cancellationToken) =>
			{
				var result = await ticketWorkflow.GetUnassignedAsync(status, pageSize, pageToken, cancellationToken);
				return DomainHttpResultMapper.ToResult(result);
			})
			.RequireAuthorization(TicketingAuthPolicies.ViewAllTickets)
			.WithName("GetUnassignedTickets")
			.WithOkDocs<PagedResult<TicketSummary>>(
				"List unassigned tickets",
				"Returns global unassigned ticket queue items for managers and admins. Optionally filter by status and page through results with pageSize and pageToken.");

		tickets.MapGet("/status/{status}", async (
				TicketStatus status,
				int? pageSize,
				string? pageToken,
				ITicketWorkflowService ticketWorkflow,
				CancellationToken cancellationToken) =>
			{
				var result = await ticketWorkflow.GetByStatusAsync(status, pageSize, pageToken, cancellationToken);
				return DomainHttpResultMapper.ToResult(result);
			})
			.RequireAuthorization(TicketingAuthPolicies.ViewAllTickets)
			.WithName("GetTicketsByStatus")
			.WithOkDocs<PagedResult<TicketSummary>>(
				"List tickets by status",
				"Returns a global status queue for managers and admins. Use this endpoint for operational lists such as all open, resolved, closed, or cancelled tickets.");

		tickets.MapGet("/team/{teamId}", async (
				string teamId,
				TicketStatus? status,
				int? pageSize,
				string? pageToken,
				ITicketWorkflowService ticketWorkflow,
				CancellationToken cancellationToken) =>
			{
				var result = await ticketWorkflow.GetTeamQueueAsync(teamId, status, pageSize, pageToken, cancellationToken);
				return DomainHttpResultMapper.ToResult(result);
			})
			.RequireAuthorization(TicketingAuthPolicies.ViewWorkQueues)
			.WithName("GetTeamTicketQueue")
			.WithOkDocs<PagedResult<TicketSummary>>(
				"List a team's ticket queue",
				"Returns tickets assigned to a specific team. Managers and admins can view any team queue; technicians can view queues for teams they belong to.",
				notFound: false);

		tickets.MapGet("/queue", async (
				string? typeId,
				string? categoryId,
				string? subcategoryId,
				TicketStatus? status,
				int? pageSize,
				string? pageToken,
				ITicketWorkflowService ticketWorkflow,
				CancellationToken cancellationToken) =>
			{
				var result = await ticketWorkflow.GetCategoryQueueAsync(
					typeId,
					categoryId,
					subcategoryId,
					status,
					pageSize,
					pageToken,
					cancellationToken);

				return DomainHttpResultMapper.ToResult(result);
			})
			.WithName("GetCategoryTicketQueue")
			.WithOkDocs<PagedResult<TicketSummary>>(
				"List a taxonomy queue",
				"Returns queue items for a type, category, or subcategory. At least one taxonomy id is required; results are filtered to tickets the caller is allowed to see.");

		tickets.MapGet("/tag/{tag}", async (
				string tag,
				TicketStatus? status,
				int? pageSize,
				string? pageToken,
				ITicketWorkflowService ticketWorkflow,
				CancellationToken cancellationToken) =>
			{
				var result = await ticketWorkflow.GetByTagAsync(tag, status, pageSize, pageToken, cancellationToken);
				return DomainHttpResultMapper.ToResult(result);
			})
			.WithName("GetTicketsByTag")
			.WithOkDocs<PagedResult<TicketSummary>>(
				"List tickets by tag",
				"Returns tickets with the requested tag, optionally filtered by status. Results are permission-filtered and returned as a paged envelope.");

		tickets.MapPut("/{ticketId}", async (
				string ticketId,
				UpdateTicketHttpRequest request,
				[FromHeader(Name = "If-Match")] string? ifMatch,
				ITicketWorkflowService ticketWorkflow,
				CancellationToken cancellationToken) =>
			{
				var result = await ticketWorkflow.UpdateAsync(
					new UpdateTicketCommand
					{
						TicketId = ticketId,
						ExpectedETag = request.ExpectedETag ?? ifMatch,
						Title = request.Title,
						Description = request.Description,
						Priority = request.Priority,
						TypeId = request.TypeId,
						CategoryId = request.CategoryId,
						SubcategoryId = request.SubcategoryId,
						ClearClassification = request.ClearClassification,
						Tags = request.Tags
					},
					cancellationToken);

				return DomainHttpResultMapper.ToResult(result);
			})
			.RequireAuthorization(TicketingAuthPolicies.Write)
			.WithName("UpdateTicket")
			.WithOkDocs<TicketRecord>(
				"Update ticket details",
				"Updates editable ticket fields such as title, description, priority, taxonomy classification, and tags. Supply ExpectedETag or If-Match to guard against concurrent edits.",
				notFound: true,
				conflict: true);

		tickets.MapPost("/{ticketId}/notes", async (
				string ticketId,
				AddTicketNoteHttpRequest request,
				ITicketWorkflowService ticketWorkflow,
				CancellationToken cancellationToken) =>
			{
				var result = await ticketWorkflow.AddNoteAsync(
					new AddTicketNoteCommand
					{
						TicketId = ticketId,
						Body = request.Body,
						IsInternal = request.IsInternal
					},
					cancellationToken);

				return DomainHttpResultMapper.ToCreated(result, note => $"/api/tickets/{note.TicketId}/notes/{note.NoteId}");
			})
			.RequireAuthorization(TicketingAuthPolicies.Write)
			.WithName("AddTicketNote")
			.WithCreatedDocs<TicketNoteRecord>(
				"Add a ticket note",
				"Adds a public or internal note. Public notes can notify submitters and workers; internal notes are visible and notified only to ticket workers.");

		tickets.MapGet("/{ticketId}/notes", async (
				string ticketId,
				int? pageSize,
				string? pageToken,
				ITicketWorkflowService ticketWorkflow,
				CancellationToken cancellationToken) =>
			{
				var result = await ticketWorkflow.GetNotesAsync(ticketId, pageSize, pageToken, cancellationToken);
				return DomainHttpResultMapper.ToResult(result);
			})
			.WithName("GetTicketNotes")
			.WithOkDocs<PagedResult<TicketNoteRecord>>(
				"List ticket notes",
				"Returns notes for a ticket. Internal notes are included only when the authenticated user can work the ticket.",
				notFound: true);

		tickets.MapPost("/{ticketId}/attachments", async (
				string ticketId,
				IFormFile file,
				TicketAttachmentUploadOptions attachmentOptions,
				ITicketWorkflowService ticketWorkflow,
				CancellationToken cancellationToken) =>
			{
				if (file.Length == 0)
				{
					return DomainHttpResultMapper.ToProblem(DomainError.Validation("Attachment content is required."));
				}

				if (file.Length > attachmentOptions.MaxSizeBytes)
				{
					return DomainHttpResultMapper.ToProblem(
						DomainError.PayloadTooLarge(
							$"Attachment size must be {FormatBytes(attachmentOptions.MaxSizeBytes)} or less."));
				}

				await using var content = file.OpenReadStream();
				var result = await ticketWorkflow.UploadAttachmentAsync(
					new UploadTicketAttachmentCommand
					{
						TicketId = ticketId,
						OriginalFileName = file.FileName,
						ContentType = file.ContentType,
						Content = content,
						SizeBytes = file.Length
					},
					cancellationToken);

				return DomainHttpResultMapper.ToCreated(
					result,
					attachment => $"/api/tickets/{attachment.TicketId}/attachments/{attachment.AttachmentId}");
			})
			.DisableAntiforgery()
			.Accepts<IFormFile>("multipart/form-data")
			.RequireAuthorization(TicketingAuthPolicies.Write)
			.WithName("UploadTicketAttachment")
			.WithUploadDocs<TicketAttachmentRecord>(
				"Upload a ticket image",
				"Uploads an image attachment using multipart/form-data. The default policy accepts common raster image formats, validates file signatures, rejects mismatched content types/extensions, and enforces the configured max upload size.");

		tickets.MapGet("/{ticketId}/attachments", async (
				string ticketId,
				int? pageSize,
				string? pageToken,
				ITicketWorkflowService ticketWorkflow,
				CancellationToken cancellationToken) =>
			{
				var result = await ticketWorkflow.GetAttachmentsAsync(ticketId, pageSize, pageToken, cancellationToken);
				return DomainHttpResultMapper.ToResult(result);
			})
			.WithName("GetTicketAttachments")
			.WithOkDocs<PagedResult<TicketAttachmentRecord>>(
				"List ticket attachments",
				"Returns non-deleted attachment metadata for a ticket. Use the content endpoint to download the image bytes.",
				notFound: true);

		tickets.MapGet("/{ticketId}/attachments/{attachmentId}/content", async (
				string ticketId,
				string attachmentId,
				ITicketWorkflowService ticketWorkflow,
				CancellationToken cancellationToken) =>
			{
				var attachmentResult = await ticketWorkflow.GetAttachmentAsync(ticketId, attachmentId, cancellationToken);
				if (attachmentResult.IsFailure)
				{
					return DomainHttpResultMapper.ToProblem(attachmentResult.Error!);
				}

				var streamResult = await ticketWorkflow.OpenAttachmentAsync(ticketId, attachmentId, cancellationToken);
				return DomainHttpResultMapper.ToFile(
					streamResult,
					attachmentResult.Value!.ContentType ?? "application/octet-stream",
					attachmentResult.Value.OriginalFileName);
			})
			.WithName("DownloadTicketAttachment")
			.WithFileDocs(
				"Download attachment content",
				"Streams the image content for a ticket attachment. The response content type and download file name come from the stored attachment metadata.");

		tickets.MapDelete("/{ticketId}/attachments/{attachmentId}", async (
				string ticketId,
				string attachmentId,
				ITicketWorkflowService ticketWorkflow,
				CancellationToken cancellationToken) =>
			{
				var result = await ticketWorkflow.DeleteAttachmentAsync(ticketId, attachmentId, cancellationToken);
				return DomainHttpResultMapper.ToNoContent(result);
			})
			.RequireAuthorization(TicketingAuthPolicies.Write)
			.RequireAuthorization(TicketingAuthPolicies.WorkTicket)
			.WithName("DeleteTicketAttachment")
			.WithNoContentDocs(
				"Delete a ticket attachment",
				"Soft-deletes an attachment, decrements the ticket attachment count, writes audit history, and enqueues email notification work. Only ticket workers can delete attachments.");

		tickets.MapGet("/{ticketId}/audit", async (
				string ticketId,
				int? pageSize,
				string? pageToken,
				ITicketWorkflowService ticketWorkflow,
				CancellationToken cancellationToken) =>
			{
				var result = await ticketWorkflow.GetAuditAsync(ticketId, pageSize, pageToken, cancellationToken);
				return DomainHttpResultMapper.ToResult(result);
			})
			.RequireAuthorization(TicketingAuthPolicies.WorkTicket)
			.WithName("GetTicketAudit")
			.WithOkDocs<PagedResult<TicketAuditEventRecord>>(
				"List ticket audit events",
				"Returns paged audit history for a ticket. Audit history is restricted to users who can work the ticket.",
				notFound: true);

		tickets.MapPost("/{ticketId}/assign", async (
				string ticketId,
				AssignTicketHttpRequest request,
				[FromHeader(Name = "If-Match")] string? ifMatch,
				ITicketWorkflowService ticketWorkflow,
				CancellationToken cancellationToken) =>
			{
				var result = await ticketWorkflow.AssignAsync(
					new AssignTicketCommand
					{
						TicketId = ticketId,
						AssigneeOid = request.AssigneeOid,
						Reason = request.Reason,
						ExpectedETag = request.ExpectedETag ?? ifMatch
					},
					cancellationToken);

				return DomainHttpResultMapper.ToResult(result);
			})
			.RequireAuthorization(TicketingAuthPolicies.Write)
			.RequireAuthorization(TicketingAuthPolicies.WorkTicket)
			.WithName("AssignTicket")
			.WithOkDocs<TicketRecord>(
				"Assign a ticket",
				"Assigns or clears the individual assignee. Assigning an open ticket moves it to InProgress. The operation writes audit history, updates queue projections, and enqueues email notification work.",
				notFound: true,
				conflict: true);

		tickets.MapPost("/{ticketId}/assign-team", async (
				string ticketId,
				AssignTicketTeamHttpRequest request,
				[FromHeader(Name = "If-Match")] string? ifMatch,
				ITicketWorkflowService ticketWorkflow,
				CancellationToken cancellationToken) =>
			{
				var result = await ticketWorkflow.AssignTeamAsync(
					new AssignTicketTeamCommand
					{
						TicketId = ticketId,
						AssignedTeamId = request.AssignedTeamId,
						Reason = request.Reason,
						ExpectedETag = request.ExpectedETag ?? ifMatch
					},
					cancellationToken);

				return DomainHttpResultMapper.ToResult(result);
			})
			.RequireAuthorization(TicketingAuthPolicies.ManageTeams)
			.WithName("AssignTicketTeam")
			.WithOkDocs<TicketRecord>(
				"Assign a ticket to a team",
				"Assigns or clears the owning team for a ticket. Managers and admins use this to route work between teams; queue projections and notification messages are updated after the change.",
				notFound: true,
				conflict: true);

		tickets.MapPost("/{ticketId}/status", async (
				string ticketId,
				SetTicketStatusHttpRequest request,
				[FromHeader(Name = "If-Match")] string? ifMatch,
				ITicketWorkflowService ticketWorkflow,
				CancellationToken cancellationToken) =>
			{
				var result = await ticketWorkflow.SetStatusAsync(
					new SetTicketStatusCommand
					{
						TicketId = ticketId,
						Status = request.Status,
						Reason = request.Reason,
						ExpectedETag = request.ExpectedETag ?? ifMatch
					},
					cancellationToken);

				return DomainHttpResultMapper.ToResult(result);
			})
			.RequireAuthorization(TicketingAuthPolicies.Write)
			.WithName("SetTicketStatus")
			.WithOkDocs<TicketRecord>(
				"Set ticket status",
				"Changes ticket status to a non-closed state. Use the close endpoint for Closed so closure timestamp and resolution note are captured consistently.",
				notFound: true,
				conflict: true);

		tickets.MapPost("/{ticketId}/start", async (
				string ticketId,
				ChangeTicketStatusHttpRequest request,
				[FromHeader(Name = "If-Match")] string? ifMatch,
				ITicketWorkflowService ticketWorkflow,
				CancellationToken cancellationToken) =>
			{
				var result = await ChangeStatusAsync(ticketId, TicketStatus.InProgress, request, ifMatch, ticketWorkflow, cancellationToken);
				return DomainHttpResultMapper.ToResult(result);
			})
			.RequireAuthorization(TicketingAuthPolicies.Write)
			.RequireAuthorization(TicketingAuthPolicies.WorkTicket)
			.WithName("StartTicket")
			.WithOkDocs<TicketRecord>(
				"Start work on a ticket",
				"Convenience workflow that moves a ticket to InProgress. Requires ticket worker access and supports ExpectedETag or If-Match concurrency checks.",
				notFound: true,
				conflict: true);

		tickets.MapPost("/{ticketId}/pending-requester", async (
				string ticketId,
				ChangeTicketStatusHttpRequest request,
				[FromHeader(Name = "If-Match")] string? ifMatch,
				ITicketWorkflowService ticketWorkflow,
				CancellationToken cancellationToken) =>
			{
				var result = await ChangeStatusAsync(ticketId, TicketStatus.PendingRequester, request, ifMatch, ticketWorkflow, cancellationToken);
				return DomainHttpResultMapper.ToResult(result);
			})
			.RequireAuthorization(TicketingAuthPolicies.Write)
			.RequireAuthorization(TicketingAuthPolicies.WorkTicket)
			.WithName("SetTicketPendingRequester")
			.WithOkDocs<TicketRecord>(
				"Mark a ticket pending requester",
				"Convenience workflow that moves a ticket to PendingRequester when the team is waiting on the requester. Requires ticket worker access.",
				notFound: true,
				conflict: true);

		tickets.MapPost("/{ticketId}/pending-vendor", async (
				string ticketId,
				ChangeTicketStatusHttpRequest request,
				[FromHeader(Name = "If-Match")] string? ifMatch,
				ITicketWorkflowService ticketWorkflow,
				CancellationToken cancellationToken) =>
			{
				var result = await ChangeStatusAsync(ticketId, TicketStatus.PendingVendor, request, ifMatch, ticketWorkflow, cancellationToken);
				return DomainHttpResultMapper.ToResult(result);
			})
			.RequireAuthorization(TicketingAuthPolicies.Write)
			.RequireAuthorization(TicketingAuthPolicies.WorkTicket)
			.WithName("SetTicketPendingVendor")
			.WithOkDocs<TicketRecord>(
				"Mark a ticket pending vendor",
				"Convenience workflow that moves a ticket to PendingVendor when the team is waiting on an external vendor. Requires ticket worker access.",
				notFound: true,
				conflict: true);

		tickets.MapPost("/{ticketId}/resolve", async (
				string ticketId,
				ChangeTicketStatusHttpRequest request,
				[FromHeader(Name = "If-Match")] string? ifMatch,
				ITicketWorkflowService ticketWorkflow,
				CancellationToken cancellationToken) =>
			{
				var result = await ChangeStatusAsync(ticketId, TicketStatus.Resolved, request, ifMatch, ticketWorkflow, cancellationToken);
				return DomainHttpResultMapper.ToResult(result);
			})
			.RequireAuthorization(TicketingAuthPolicies.Write)
			.RequireAuthorization(TicketingAuthPolicies.WorkTicket)
			.WithName("ResolveTicket")
			.WithOkDocs<TicketRecord>(
				"Resolve a ticket",
				"Convenience workflow that moves a ticket to Resolved. Requires ticket worker access and can include a reason for audit/notification context.",
				notFound: true,
				conflict: true);

		tickets.MapPost("/{ticketId}/cancel", async (
				string ticketId,
				ChangeTicketStatusHttpRequest request,
				[FromHeader(Name = "If-Match")] string? ifMatch,
				ITicketWorkflowService ticketWorkflow,
				CancellationToken cancellationToken) =>
			{
				var result = await ChangeStatusAsync(ticketId, TicketStatus.Cancelled, request, ifMatch, ticketWorkflow, cancellationToken);
				return DomainHttpResultMapper.ToResult(result);
			})
			.RequireAuthorization(TicketingAuthPolicies.Write)
			.WithName("CancelTicket")
			.WithOkDocs<TicketRecord>(
				"Cancel a ticket",
				"Moves a ticket to Cancelled. Ticket workers can cancel tickets they can work; submitters can cancel their own tickets.",
				notFound: true,
				conflict: true);

		tickets.MapPost("/{ticketId}/close", async (
				string ticketId,
				CloseTicketHttpRequest request,
				[FromHeader(Name = "If-Match")] string? ifMatch,
				ITicketWorkflowService ticketWorkflow,
				CancellationToken cancellationToken) =>
			{
				var result = await ticketWorkflow.CloseAsync(
					new CloseTicketCommand
					{
						TicketId = ticketId,
						ResolutionNote = request.ResolutionNote,
						ExpectedETag = request.ExpectedETag ?? ifMatch
					},
					cancellationToken);

				return DomainHttpResultMapper.ToResult(result);
			})
			.RequireAuthorization(TicketingAuthPolicies.Write)
			.RequireAuthorization(TicketingAuthPolicies.WorkTicket)
			.WithName("CloseTicket")
			.WithOkDocs<TicketRecord>(
				"Close a ticket",
				"Closes a ticket, records the closure timestamp, stores the optional resolution note in audit context, and enqueues email notification work. Requires ticket worker access.",
				notFound: true,
				conflict: true);

		tickets.MapPost("/{ticketId}/reopen", async (
				string ticketId,
				ReopenTicketHttpRequest request,
				[FromHeader(Name = "If-Match")] string? ifMatch,
				ITicketWorkflowService ticketWorkflow,
				CancellationToken cancellationToken) =>
			{
				var result = await ticketWorkflow.ReopenAsync(
					new ReopenTicketCommand
					{
						TicketId = ticketId,
						Reason = request.Reason,
						ExpectedETag = request.ExpectedETag ?? ifMatch
					},
					cancellationToken);

				return DomainHttpResultMapper.ToResult(result);
			})
			.RequireAuthorization(TicketingAuthPolicies.Write)
			.WithName("ReopenTicket")
			.WithOkDocs<TicketRecord>(
				"Reopen a ticket",
				"Reopens a ticket and clears the closure timestamp. Reopened tickets return to Open when unassigned or InProgress when an assignee exists.",
				notFound: true,
				conflict: true);

		return tickets;
	}

	private static Task<DomainResult<TicketRecord>> ChangeStatusAsync(
		string ticketId,
		TicketStatus status,
		ChangeTicketStatusHttpRequest request,
		string? ifMatch,
		ITicketWorkflowService ticketWorkflow,
		CancellationToken cancellationToken) =>
		ticketWorkflow.SetStatusAsync(
			new SetTicketStatusCommand
			{
				TicketId = ticketId,
				Status = status,
				Reason = request.Reason,
				ExpectedETag = request.ExpectedETag ?? ifMatch
			},
			cancellationToken);

	private static string FormatBytes(long bytes)
	{
		const long mebibyte = 1024 * 1024;
		return bytes >= mebibyte && bytes % mebibyte == 0
			? $"{bytes / mebibyte} MiB"
			: $"{bytes:N0} bytes";
	}
}
