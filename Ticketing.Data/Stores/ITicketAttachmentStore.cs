using Ticketing.Data.Models;

namespace Ticketing.Data.Stores;

public interface ITicketAttachmentStore
{
	Task<TicketAttachmentRecord> UploadAsync(UploadTicketAttachmentRequest request, CancellationToken cancellationToken = default);

	IAsyncEnumerable<TicketAttachmentRecord> GetForTicketAsync(
		string ticketId,
		bool includeDeleted = false,
		int? pageSize = null,
		CancellationToken cancellationToken = default);

	Task<Stream> OpenReadAsync(string ticketId, string attachmentId, CancellationToken cancellationToken = default);

	Task SoftDeleteAsync(string ticketId, string attachmentId, string deletedByOid, CancellationToken cancellationToken = default);
}
