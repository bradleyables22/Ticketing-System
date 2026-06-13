using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Ticketing.Auth;
using Ticketing.Data.Models;
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
			.WithName("CreateTicket");

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
						PageSize = pageSize
					},
					cancellationToken);

				return DomainHttpResultMapper.ToResult(result);
			})
			.WithName("SearchTickets");

		tickets.MapGet("/{ticketId}", async (
				string ticketId,
				ITicketWorkflowService ticketWorkflow,
				CancellationToken cancellationToken) =>
			{
				var result = await ticketWorkflow.GetAsync(ticketId, cancellationToken);
				return DomainHttpResultMapper.ToResult(result);
			})
			.WithName("GetTicket");

		tickets.MapGet("/by-number/{ticketNumber}", async (
				string ticketNumber,
				ITicketWorkflowService ticketWorkflow,
				CancellationToken cancellationToken) =>
			{
				var result = await ticketWorkflow.GetByNumberAsync(ticketNumber, cancellationToken);
				return DomainHttpResultMapper.ToResult(result);
			})
			.WithName("GetTicketByNumber");

		tickets.MapGet("/mine", async (
				TicketStatus? status,
				int? pageSize,
				ITicketWorkflowService ticketWorkflow,
				CancellationToken cancellationToken) =>
			{
				var result = await ticketWorkflow.GetMyTicketsAsync(status, pageSize, cancellationToken);
				return DomainHttpResultMapper.ToResult(result);
			})
			.WithName("GetMyTickets");

		tickets.MapGet("/assigned-to-me", async (
				TicketStatus? status,
				int? pageSize,
				ITicketWorkflowService ticketWorkflow,
				CancellationToken cancellationToken) =>
			{
				var result = await ticketWorkflow.GetAssignedToMeAsync(status, pageSize, cancellationToken);
				return DomainHttpResultMapper.ToResult(result);
			})
			.RequireAuthorization(TicketingAuthPolicies.ViewWorkQueues)
			.WithName("GetAssignedToMe");

		tickets.MapGet("/unassigned", async (
				TicketStatus? status,
				int? pageSize,
				ITicketWorkflowService ticketWorkflow,
				CancellationToken cancellationToken) =>
			{
				var result = await ticketWorkflow.GetUnassignedAsync(status, pageSize, cancellationToken);
				return DomainHttpResultMapper.ToResult(result);
			})
			.RequireAuthorization(TicketingAuthPolicies.ViewAllTickets)
			.WithName("GetUnassignedTickets");

		tickets.MapGet("/status/{status}", async (
				TicketStatus status,
				int? pageSize,
				ITicketWorkflowService ticketWorkflow,
				CancellationToken cancellationToken) =>
			{
				var result = await ticketWorkflow.GetByStatusAsync(status, pageSize, cancellationToken);
				return DomainHttpResultMapper.ToResult(result);
			})
			.RequireAuthorization(TicketingAuthPolicies.ViewAllTickets)
			.WithName("GetTicketsByStatus");

		tickets.MapGet("/team/{teamId}", async (
				string teamId,
				TicketStatus? status,
				int? pageSize,
				ITicketWorkflowService ticketWorkflow,
				CancellationToken cancellationToken) =>
			{
				var result = await ticketWorkflow.GetTeamQueueAsync(teamId, status, pageSize, cancellationToken);
				return DomainHttpResultMapper.ToResult(result);
			})
			.RequireAuthorization(TicketingAuthPolicies.ViewWorkQueues)
			.WithName("GetTeamTicketQueue");

		tickets.MapGet("/queue", async (
				string? typeId,
				string? categoryId,
				string? subcategoryId,
				TicketStatus? status,
				int? pageSize,
				ITicketWorkflowService ticketWorkflow,
				CancellationToken cancellationToken) =>
			{
				var result = await ticketWorkflow.GetCategoryQueueAsync(
					typeId,
					categoryId,
					subcategoryId,
					status,
					pageSize,
					cancellationToken);

				return DomainHttpResultMapper.ToResult(result);
			})
			.WithName("GetCategoryTicketQueue");

		tickets.MapGet("/tag/{tag}", async (
				string tag,
				TicketStatus? status,
				int? pageSize,
				ITicketWorkflowService ticketWorkflow,
				CancellationToken cancellationToken) =>
			{
				var result = await ticketWorkflow.GetByTagAsync(tag, status, pageSize, cancellationToken);
				return DomainHttpResultMapper.ToResult(result);
			})
			.WithName("GetTicketsByTag");

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
			.WithName("UpdateTicket");

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
			.WithName("AddTicketNote");

		tickets.MapGet("/{ticketId}/notes", async (
				string ticketId,
				int? pageSize,
				ITicketWorkflowService ticketWorkflow,
				CancellationToken cancellationToken) =>
			{
				var result = await ticketWorkflow.GetNotesAsync(ticketId, pageSize, cancellationToken);
				return DomainHttpResultMapper.ToResult(result);
			})
			.WithName("GetTicketNotes");

		tickets.MapPost("/{ticketId}/attachments", async (
				string ticketId,
				IFormFile file,
				ITicketWorkflowService ticketWorkflow,
				CancellationToken cancellationToken) =>
			{
				if (file.Length == 0)
				{
					return DomainHttpResultMapper.ToProblem(DomainError.Validation("Attachment content is required."));
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
			.RequireAuthorization(TicketingAuthPolicies.Write)
			.WithName("UploadTicketAttachment");

		tickets.MapGet("/{ticketId}/attachments", async (
				string ticketId,
				int? pageSize,
				ITicketWorkflowService ticketWorkflow,
				CancellationToken cancellationToken) =>
			{
				var result = await ticketWorkflow.GetAttachmentsAsync(ticketId, pageSize, cancellationToken);
				return DomainHttpResultMapper.ToResult(result);
			})
			.WithName("GetTicketAttachments");

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
			.WithName("DownloadTicketAttachment");

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
			.WithName("DeleteTicketAttachment");

		tickets.MapGet("/{ticketId}/audit", async (
				string ticketId,
				int? pageSize,
				ITicketWorkflowService ticketWorkflow,
				CancellationToken cancellationToken) =>
			{
				var result = await ticketWorkflow.GetAuditAsync(ticketId, pageSize, cancellationToken);
				return DomainHttpResultMapper.ToResult(result);
			})
			.RequireAuthorization(TicketingAuthPolicies.WorkTicket)
			.WithName("GetTicketAudit");

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
			.WithName("AssignTicket");

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
			.WithName("AssignTicketTeam");

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
			.WithName("SetTicketStatus");

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
			.WithName("StartTicket");

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
			.WithName("SetTicketPendingRequester");

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
			.WithName("SetTicketPendingVendor");

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
			.WithName("ResolveTicket");

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
			.WithName("CancelTicket");

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
			.WithName("CloseTicket");

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
			.WithName("ReopenTicket");

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
}
