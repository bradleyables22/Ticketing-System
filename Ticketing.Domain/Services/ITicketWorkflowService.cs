using Ticketing.Data.Models;
using Ticketing.Domain.Models;

namespace Ticketing.Domain.Services;

public interface ITicketWorkflowService
{
	Task<DomainResult<TicketRecord>> CreateAsync(CreateTicketCommand command, CancellationToken cancellationToken = default);

	Task<DomainResult<TicketRecord>> GetAsync(string ticketId, CancellationToken cancellationToken = default);

	Task<DomainResult<TicketRecord>> GetByNumberAsync(string ticketNumber, CancellationToken cancellationToken = default);

	Task<DomainResult<PagedResult<TicketSummary>>> GetMyTicketsAsync(
		TicketStatus? status = null,
		int? pageSize = null,
		string? pageToken = null,
		CancellationToken cancellationToken = default);

	Task<DomainResult<PagedResult<TicketSummary>>> GetAssignedToMeAsync(
		TicketStatus? status = null,
		int? pageSize = null,
		string? pageToken = null,
		CancellationToken cancellationToken = default);

	Task<DomainResult<PagedResult<TicketSummary>>> GetUnassignedAsync(
		TicketStatus? status = null,
		int? pageSize = null,
		string? pageToken = null,
		CancellationToken cancellationToken = default);

	Task<DomainResult<PagedResult<TicketSummary>>> GetByStatusAsync(
		TicketStatus status,
		int? pageSize = null,
		string? pageToken = null,
		CancellationToken cancellationToken = default);

	Task<DomainResult<PagedResult<TicketSummary>>> GetTeamQueueAsync(
		string teamId,
		TicketStatus? status = null,
		int? pageSize = null,
		string? pageToken = null,
		CancellationToken cancellationToken = default);

	Task<DomainResult<PagedResult<TicketSummary>>> GetCategoryQueueAsync(
		string? typeId,
		string? categoryId,
		string? subcategoryId,
		TicketStatus? status = null,
		int? pageSize = null,
		string? pageToken = null,
		CancellationToken cancellationToken = default);

	Task<DomainResult<PagedResult<TicketSummary>>> GetByTagAsync(
		string tag,
		TicketStatus? status = null,
		int? pageSize = null,
		string? pageToken = null,
		CancellationToken cancellationToken = default);

	Task<DomainResult<PagedResult<TicketSummary>>> SearchAsync(
		TicketSearchCriteria criteria,
		CancellationToken cancellationToken = default);

	Task<DomainResult<TicketRecord>> UpdateAsync(UpdateTicketCommand command, CancellationToken cancellationToken = default);

	Task<DomainResult<TicketNoteRecord>> AddNoteAsync(AddTicketNoteCommand command, CancellationToken cancellationToken = default);

	Task<DomainResult<PagedResult<TicketNoteRecord>>> GetNotesAsync(
		string ticketId,
		int? pageSize = null,
		string? pageToken = null,
		CancellationToken cancellationToken = default);

	Task<DomainResult<TicketAttachmentRecord>> UploadAttachmentAsync(
		UploadTicketAttachmentCommand command,
		CancellationToken cancellationToken = default);

	Task<DomainResult<PagedResult<TicketAttachmentRecord>>> GetAttachmentsAsync(
		string ticketId,
		int? pageSize = null,
		string? pageToken = null,
		CancellationToken cancellationToken = default);

	Task<DomainResult<TicketAttachmentRecord>> GetAttachmentAsync(
		string ticketId,
		string attachmentId,
		CancellationToken cancellationToken = default);

	Task<DomainResult<Stream>> OpenAttachmentAsync(
		string ticketId,
		string attachmentId,
		CancellationToken cancellationToken = default);

	Task<DomainResult> DeleteAttachmentAsync(
		string ticketId,
		string attachmentId,
		CancellationToken cancellationToken = default);

	Task<DomainResult<PagedResult<TicketAuditEventRecord>>> GetAuditAsync(
		string ticketId,
		int? pageSize = null,
		string? pageToken = null,
		CancellationToken cancellationToken = default);

	Task<DomainResult<TicketRecord>> AssignAsync(AssignTicketCommand command, CancellationToken cancellationToken = default);

	Task<DomainResult<TicketRecord>> AssignTeamAsync(AssignTicketTeamCommand command, CancellationToken cancellationToken = default);

	Task<DomainResult<TicketRecord>> SetStatusAsync(SetTicketStatusCommand command, CancellationToken cancellationToken = default);

	Task<DomainResult<TicketRecord>> CloseAsync(CloseTicketCommand command, CancellationToken cancellationToken = default);

	Task<DomainResult<TicketRecord>> ReopenAsync(ReopenTicketCommand command, CancellationToken cancellationToken = default);
}
