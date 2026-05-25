using Ticketing.Data.Models;
using Ticketing.Domain.Models;

namespace Ticketing.Domain.Services;

public interface ITicketWorkflowService
{
	Task<TicketRecord> CreateAsync(CreateTicketCommand command, CancellationToken cancellationToken = default);

	Task<TicketRecord> GetAsync(string ticketId, CancellationToken cancellationToken = default);

	Task<TicketRecord> GetByNumberAsync(string ticketNumber, CancellationToken cancellationToken = default);

	IAsyncEnumerable<TicketSummary> GetMyTicketsAsync(
		TicketStatus? status = null,
		int? pageSize = null,
		CancellationToken cancellationToken = default);

	IAsyncEnumerable<TicketSummary> GetAssignedToMeAsync(
		TicketStatus? status = null,
		int? pageSize = null,
		CancellationToken cancellationToken = default);

	IAsyncEnumerable<TicketSummary> GetTeamQueueAsync(
		string teamId,
		TicketStatus? status = null,
		int? pageSize = null,
		CancellationToken cancellationToken = default);

	Task<TicketRecord> UpdateAsync(UpdateTicketCommand command, CancellationToken cancellationToken = default);

	Task<TicketNoteRecord> AddNoteAsync(AddTicketNoteCommand command, CancellationToken cancellationToken = default);

	IAsyncEnumerable<TicketNoteRecord> GetNotesAsync(
		string ticketId,
		int? pageSize = null,
		CancellationToken cancellationToken = default);

	Task<TicketAttachmentRecord> UploadAttachmentAsync(
		UploadTicketAttachmentCommand command,
		CancellationToken cancellationToken = default);

	IAsyncEnumerable<TicketAttachmentRecord> GetAttachmentsAsync(
		string ticketId,
		int? pageSize = null,
		CancellationToken cancellationToken = default);

	Task<Stream> OpenAttachmentAsync(
		string ticketId,
		string attachmentId,
		CancellationToken cancellationToken = default);

	Task DeleteAttachmentAsync(
		string ticketId,
		string attachmentId,
		CancellationToken cancellationToken = default);

	IAsyncEnumerable<TicketAuditEventRecord> GetAuditAsync(
		string ticketId,
		int? pageSize = null,
		CancellationToken cancellationToken = default);

	Task<TicketRecord> AssignAsync(AssignTicketCommand command, CancellationToken cancellationToken = default);

	Task<TicketRecord> AssignTeamAsync(AssignTicketTeamCommand command, CancellationToken cancellationToken = default);

	Task<TicketRecord> CloseAsync(CloseTicketCommand command, CancellationToken cancellationToken = default);

	Task<TicketRecord> ReopenAsync(ReopenTicketCommand command, CancellationToken cancellationToken = default);
}
