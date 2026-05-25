using Ticketing.Data.Models;
using Ticketing.Data.Stores;
using Ticketing.Domain.Exceptions;
using Ticketing.Domain.Models;

namespace Ticketing.Domain.Services;

internal sealed class TicketWorkflowService : ITicketWorkflowService
{
	private readonly CurrentUserService _currentUser;
	private readonly ITicketStore _ticketStore;
	private readonly ITicketQueryStore _ticketQueryStore;
	private readonly ITicketNoteStore _ticketNoteStore;
	private readonly ITicketAttachmentStore _ticketAttachmentStore;
	private readonly ITicketAuditStore _ticketAuditStore;
	private readonly ITicketPermissionService _permissions;
	private readonly ITeamStore _teamStore;

	public TicketWorkflowService(
		CurrentUserService currentUser,
		ITicketStore ticketStore,
		ITicketQueryStore ticketQueryStore,
		ITicketNoteStore ticketNoteStore,
		ITicketAttachmentStore ticketAttachmentStore,
		ITicketAuditStore ticketAuditStore,
		ITicketPermissionService permissions,
		ITeamStore teamStore)
	{
		_currentUser = currentUser;
		_ticketStore = ticketStore;
		_ticketQueryStore = ticketQueryStore;
		_ticketNoteStore = ticketNoteStore;
		_ticketAttachmentStore = ticketAttachmentStore;
		_ticketAuditStore = ticketAuditStore;
		_permissions = permissions;
		_teamStore = teamStore;
	}

	public async Task<TicketRecord> CreateAsync(CreateTicketCommand command, CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(command.Title);
		ArgumentException.ThrowIfNullOrWhiteSpace(command.Description);

		var userOid = await _currentUser.RequireUserOidAndSyncProfileAsync(cancellationToken);

		return await _ticketStore.CreateAsync(
			new CreateTicketRequest
			{
				Title = command.Title,
				Description = command.Description,
				SubmitterOid = userOid,
				Priority = command.Priority,
				TypeId = command.TypeId,
				CategoryId = command.CategoryId,
				SubcategoryId = command.SubcategoryId,
				Tags = command.Tags
			},
			cancellationToken);
	}

	public async Task<TicketRecord> GetAsync(string ticketId, CancellationToken cancellationToken = default)
	{
		var ticket = await GetRequiredTicketAsync(ticketId, cancellationToken);
		await EnsureCanViewAsync(ticket, cancellationToken);
		return ticket;
	}

	public async Task<TicketRecord> GetByNumberAsync(string ticketNumber, CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(ticketNumber);

		var ticket = await _ticketStore.GetByNumberAsync(ticketNumber, cancellationToken)
			?? throw new TicketingNotFoundException("Ticket", ticketNumber);

		await EnsureCanViewAsync(ticket, cancellationToken);
		return ticket;
	}

	public IAsyncEnumerable<TicketSummary> GetMyTicketsAsync(
		TicketStatus? status = null,
		int? pageSize = null,
		CancellationToken cancellationToken = default)
	{
		var userOid = _currentUser.RequireUserOid();
		return _ticketQueryStore.GetSubmittedAsync(userOid, status, pageSize, cancellationToken);
	}

	public IAsyncEnumerable<TicketSummary> GetAssignedToMeAsync(
		TicketStatus? status = null,
		int? pageSize = null,
		CancellationToken cancellationToken = default)
	{
		var userOid = _currentUser.RequireUserOid();
		if (!_permissions.IsTechnicianOrAbove())
		{
			throw new TicketingForbiddenException("Only technicians, managers, and admins can view assigned work queues.");
		}

		return _ticketQueryStore.GetAssignedAsync(userOid, status, pageSize, cancellationToken);
	}

	public async IAsyncEnumerable<TicketSummary> GetTeamQueueAsync(
		string teamId,
		TicketStatus? status = null,
		int? pageSize = null,
		[System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(teamId);
		var userOid = _currentUser.RequireUserOid();

		if (!_permissions.CanManageTeams()
			&& (!_permissions.IsTechnicianOrAbove()
				|| !await IsOnTeamAsync(userOid, teamId, cancellationToken)))
		{
			throw new TicketingForbiddenException("You do not have access to this team queue.");
		}

		await foreach (var ticket in _ticketQueryStore.GetByTeamAsync(teamId, status, pageSize, cancellationToken))
		{
			yield return ticket;
		}
	}

	public async Task<TicketRecord> UpdateAsync(UpdateTicketCommand command, CancellationToken cancellationToken = default)
	{
		var ticket = await GetRequiredTicketAsync(command.TicketId, cancellationToken);
		if (!await _permissions.CanWorkTicketAsync(ticket, cancellationToken))
		{
			throw new TicketingForbiddenException("You do not have permission to update this ticket.");
		}

		var userOid = await _currentUser.RequireUserOidAndSyncProfileAsync(cancellationToken);
		return await _ticketStore.UpdateDetailsAsync(
			new UpdateTicketDetailsRequest
			{
				TicketId = command.TicketId,
				UpdatedByOid = userOid,
				ExpectedETag = command.ExpectedETag,
				Title = command.Title,
				Description = command.Description,
				Priority = command.Priority,
				TypeId = command.TypeId,
				CategoryId = command.CategoryId,
				SubcategoryId = command.SubcategoryId,
				ClearClassification = command.ClearClassification,
				Tags = command.Tags
			},
			cancellationToken);
	}

	public async Task<TicketNoteRecord> AddNoteAsync(AddTicketNoteCommand command, CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(command.Body);

		var ticket = await GetRequiredTicketAsync(command.TicketId, cancellationToken);
		var canWork = await _permissions.CanWorkTicketAsync(ticket, cancellationToken);
		var canView = canWork || await _permissions.CanViewTicketAsync(ticket, cancellationToken);

		if (!canView)
		{
			throw new TicketingForbiddenException("You do not have permission to add a note to this ticket.");
		}

		if (command.IsInternal && !canWork)
		{
			throw new TicketingForbiddenException("Only ticket workers can add internal notes.");
		}

		var userOid = await _currentUser.RequireUserOidAndSyncProfileAsync(cancellationToken);
		return await _ticketNoteStore.AddAsync(
			new AddTicketNoteRequest
			{
				TicketId = command.TicketId,
				AuthorOid = userOid,
				Body = command.Body,
				IsInternal = command.IsInternal
			},
			cancellationToken);
	}

	public async IAsyncEnumerable<TicketNoteRecord> GetNotesAsync(
		string ticketId,
		int? pageSize = null,
		[System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		var ticket = await GetRequiredTicketAsync(ticketId, cancellationToken);
		await EnsureCanViewAsync(ticket, cancellationToken);
		var includeInternal = await _permissions.CanWorkTicketAsync(ticket, cancellationToken);

		await foreach (var note in _ticketNoteStore.GetForTicketAsync(ticketId, includeInternal, pageSize, cancellationToken))
		{
			yield return note;
		}
	}

	public async Task<TicketAttachmentRecord> UploadAttachmentAsync(
		UploadTicketAttachmentCommand command,
		CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(command.OriginalFileName);

		var ticket = await GetRequiredTicketAsync(command.TicketId, cancellationToken);
		await EnsureCanViewAsync(ticket, cancellationToken);

		var userOid = await _currentUser.RequireUserOidAndSyncProfileAsync(cancellationToken);
		return await _ticketAttachmentStore.UploadAsync(
			new UploadTicketAttachmentRequest
			{
				TicketId = command.TicketId,
				UploadedByOid = userOid,
				OriginalFileName = command.OriginalFileName,
				ContentType = command.ContentType,
				Content = command.Content,
				SizeBytes = command.SizeBytes
			},
			cancellationToken);
	}

	public async IAsyncEnumerable<TicketAttachmentRecord> GetAttachmentsAsync(
		string ticketId,
		int? pageSize = null,
		[System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		var ticket = await GetRequiredTicketAsync(ticketId, cancellationToken);
		await EnsureCanViewAsync(ticket, cancellationToken);

		await foreach (var attachment in _ticketAttachmentStore.GetForTicketAsync(ticketId, includeDeleted: false, pageSize, cancellationToken))
		{
			yield return attachment;
		}
	}

	public async Task<Stream> OpenAttachmentAsync(
		string ticketId,
		string attachmentId,
		CancellationToken cancellationToken = default)
	{
		var ticket = await GetRequiredTicketAsync(ticketId, cancellationToken);
		await EnsureCanViewAsync(ticket, cancellationToken);
		return await _ticketAttachmentStore.OpenReadAsync(ticketId, attachmentId, cancellationToken);
	}

	public async Task DeleteAttachmentAsync(
		string ticketId,
		string attachmentId,
		CancellationToken cancellationToken = default)
	{
		var ticket = await GetRequiredTicketAsync(ticketId, cancellationToken);
		if (!await _permissions.CanWorkTicketAsync(ticket, cancellationToken))
		{
			throw new TicketingForbiddenException("Only ticket workers can delete attachments.");
		}

		var userOid = await _currentUser.RequireUserOidAndSyncProfileAsync(cancellationToken);
		await _ticketAttachmentStore.SoftDeleteAsync(ticketId, attachmentId, userOid, cancellationToken);
	}

	public async IAsyncEnumerable<TicketAuditEventRecord> GetAuditAsync(
		string ticketId,
		int? pageSize = null,
		[System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		var ticket = await GetRequiredTicketAsync(ticketId, cancellationToken);
		if (!await _permissions.CanWorkTicketAsync(ticket, cancellationToken))
		{
			throw new TicketingForbiddenException("Only ticket workers can view audit history.");
		}

		await foreach (var auditEvent in _ticketAuditStore.GetForTicketAsync(ticketId, pageSize, cancellationToken))
		{
			yield return auditEvent;
		}
	}

	public async Task<TicketRecord> AssignAsync(AssignTicketCommand command, CancellationToken cancellationToken = default)
	{
		var ticket = await GetRequiredTicketAsync(command.TicketId, cancellationToken);
		if (!await _permissions.CanAssignTicketAsync(ticket, command.AssigneeOid, cancellationToken))
		{
			throw new TicketingForbiddenException("You do not have permission to assign this ticket.");
		}

		var userOid = await _currentUser.RequireUserOidAndSyncProfileAsync(cancellationToken);
		return await _ticketStore.AssignAsync(
			new AssignTicketRequest
			{
				TicketId = command.TicketId,
				ChangedByOid = userOid,
				AssigneeOid = command.AssigneeOid,
				Reason = command.Reason,
				ExpectedETag = command.ExpectedETag
			},
			cancellationToken);
	}

	public async Task<TicketRecord> AssignTeamAsync(AssignTicketTeamCommand command, CancellationToken cancellationToken = default)
	{
		var ticket = await GetRequiredTicketAsync(command.TicketId, cancellationToken);
		if (!await _permissions.CanAssignTicketTeamAsync(ticket, command.AssignedTeamId, cancellationToken))
		{
			throw new TicketingForbiddenException("Only managers and admins can reassign tickets between teams.");
		}

		var userOid = await _currentUser.RequireUserOidAndSyncProfileAsync(cancellationToken);
		return await _ticketStore.AssignTeamAsync(
			new AssignTicketTeamRequest
			{
				TicketId = command.TicketId,
				ChangedByOid = userOid,
				AssignedTeamId = command.AssignedTeamId,
				Reason = command.Reason,
				ExpectedETag = command.ExpectedETag
			},
			cancellationToken);
	}

	public async Task<TicketRecord> CloseAsync(CloseTicketCommand command, CancellationToken cancellationToken = default)
	{
		var ticket = await GetRequiredTicketAsync(command.TicketId, cancellationToken);
		if (!await _permissions.CanWorkTicketAsync(ticket, cancellationToken))
		{
			throw new TicketingForbiddenException("You do not have permission to close this ticket.");
		}

		var userOid = await _currentUser.RequireUserOidAndSyncProfileAsync(cancellationToken);
		return await _ticketStore.CloseAsync(
			new CloseTicketRequest
			{
				TicketId = command.TicketId,
				ClosedByOid = userOid,
				ResolutionNote = command.ResolutionNote,
				ExpectedETag = command.ExpectedETag
			},
			cancellationToken);
	}

	public async Task<TicketRecord> ReopenAsync(ReopenTicketCommand command, CancellationToken cancellationToken = default)
	{
		var ticket = await GetRequiredTicketAsync(command.TicketId, cancellationToken);
		if (!await _permissions.CanViewTicketAsync(ticket, cancellationToken))
		{
			throw new TicketingForbiddenException("You do not have permission to reopen this ticket.");
		}

		var userOid = await _currentUser.RequireUserOidAndSyncProfileAsync(cancellationToken);
		return await _ticketStore.ReopenAsync(
			new ReopenTicketRequest
			{
				TicketId = command.TicketId,
				ReopenedByOid = userOid,
				Reason = command.Reason,
				ExpectedETag = command.ExpectedETag
			},
			cancellationToken);
	}

	private async Task<TicketRecord> GetRequiredTicketAsync(string ticketId, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(ticketId);
		return await _ticketStore.GetAsync(ticketId, cancellationToken)
			?? throw new TicketingNotFoundException("Ticket", ticketId);
	}

	private async Task EnsureCanViewAsync(TicketRecord ticket, CancellationToken cancellationToken)
	{
		if (!await _permissions.CanViewTicketAsync(ticket, cancellationToken))
		{
			throw new TicketingForbiddenException("You do not have permission to view this ticket.");
		}
	}

	private async Task<bool> IsOnTeamAsync(string userOid, string teamId, CancellationToken cancellationToken)
	{
		return await _teamStore.IsUserOnTeamAsync(userOid, teamId, cancellationToken);
	}
}
