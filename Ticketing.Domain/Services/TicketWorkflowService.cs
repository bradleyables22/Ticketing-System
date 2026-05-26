using System.Runtime.CompilerServices;
using Ticketing.Data.Models;
using Ticketing.Data.Stores;
using Ticketing.Domain.Exceptions;
using Ticketing.Domain.Models;

namespace Ticketing.Domain.Services;

internal sealed class TicketWorkflowService : ITicketWorkflowService
{
	private const int DefaultPageSize = 50;
	private const int MaxPageSize = 500;

	private static readonly TicketStatus[] SearchStatuses =
	[
		TicketStatus.Open,
		TicketStatus.InProgress,
		TicketStatus.PendingRequester,
		TicketStatus.PendingVendor,
		TicketStatus.Resolved,
		TicketStatus.Closed,
		TicketStatus.Cancelled
	];

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

	public Task<DomainResult<TicketRecord>> CreateAsync(CreateTicketCommand command, CancellationToken cancellationToken = default) =>
		DomainResult<TicketRecord>.TryAsync(async () =>
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
		});

	public Task<DomainResult<TicketRecord>> GetAsync(string ticketId, CancellationToken cancellationToken = default) =>
		DomainResult<TicketRecord>.TryAsync(async () =>
		{
			var ticket = await GetRequiredTicketAsync(ticketId, cancellationToken);
			await EnsureCanViewAsync(ticket, cancellationToken);
			return ticket;
		});

	public Task<DomainResult<TicketRecord>> GetByNumberAsync(string ticketNumber, CancellationToken cancellationToken = default) =>
		DomainResult<TicketRecord>.TryAsync(async () =>
		{
			ArgumentException.ThrowIfNullOrWhiteSpace(ticketNumber);

			var ticket = await _ticketStore.GetByNumberAsync(ticketNumber, cancellationToken)
				?? throw new TicketingNotFoundException("Ticket", ticketNumber);

			await EnsureCanViewAsync(ticket, cancellationToken);
			return ticket;
		});

	public Task<DomainResult<IReadOnlyList<TicketSummary>>> GetMyTicketsAsync(
		TicketStatus? status = null,
		int? pageSize = null,
		CancellationToken cancellationToken = default) =>
		DomainResult<IReadOnlyList<TicketSummary>>.TryAsync(async () =>
		{
			var userOid = _currentUser.RequireUserOid();
			return await _ticketQueryStore.GetSubmittedAsync(userOid, status, pageSize, cancellationToken)
				.ToReadOnlyListAsync(cancellationToken);
		});

	public Task<DomainResult<IReadOnlyList<TicketSummary>>> GetAssignedToMeAsync(
		TicketStatus? status = null,
		int? pageSize = null,
		CancellationToken cancellationToken = default) =>
		DomainResult<IReadOnlyList<TicketSummary>>.TryAsync(async () =>
		{
			var userOid = _currentUser.RequireUserOid();
			if (!_permissions.IsTechnicianOrAbove())
			{
				throw new TicketingForbiddenException("Only technicians, managers, and admins can view assigned work queues.");
			}

			return await _ticketQueryStore.GetAssignedAsync(userOid, status, pageSize, cancellationToken)
				.ToReadOnlyListAsync(cancellationToken);
		});

	public Task<DomainResult<IReadOnlyList<TicketSummary>>> GetUnassignedAsync(
		TicketStatus? status = null,
		int? pageSize = null,
		CancellationToken cancellationToken = default) =>
		DomainResult<IReadOnlyList<TicketSummary>>.TryAsync(async () =>
		{
			EnsureCanViewAllTickets("Only managers and admins can view the global unassigned queue.");
			return await _ticketQueryStore.GetUnassignedAsync(status, pageSize, cancellationToken)
				.ToReadOnlyListAsync(cancellationToken);
		});

	public Task<DomainResult<IReadOnlyList<TicketSummary>>> GetByStatusAsync(
		TicketStatus status,
		int? pageSize = null,
		CancellationToken cancellationToken = default) =>
		DomainResult<IReadOnlyList<TicketSummary>>.TryAsync(async () =>
		{
			EnsureCanViewAllTickets("Only managers and admins can view global status queues.");
			return await _ticketQueryStore.GetByStatusAsync(status, pageSize, cancellationToken)
				.ToReadOnlyListAsync(cancellationToken);
		});

	public Task<DomainResult<IReadOnlyList<TicketSummary>>> GetTeamQueueAsync(
		string teamId,
		TicketStatus? status = null,
		int? pageSize = null,
		CancellationToken cancellationToken = default) =>
		DomainResult<IReadOnlyList<TicketSummary>>.TryAsync(async () =>
		{
			ArgumentException.ThrowIfNullOrWhiteSpace(teamId);
			var userOid = _currentUser.RequireUserOid();

			if (!_permissions.CanManageTeams()
				&& (!_permissions.IsTechnicianOrAbove()
					|| !await IsOnTeamAsync(userOid, teamId, cancellationToken)))
			{
				throw new TicketingForbiddenException("You do not have access to this team queue.");
			}

			return await _ticketQueryStore.GetByTeamAsync(teamId, status, pageSize, cancellationToken)
				.ToReadOnlyListAsync(cancellationToken);
		});

	public Task<DomainResult<IReadOnlyList<TicketSummary>>> GetCategoryQueueAsync(
		string? typeId,
		string? categoryId,
		string? subcategoryId,
		TicketStatus? status = null,
		int? pageSize = null,
		CancellationToken cancellationToken = default) =>
		DomainResult<IReadOnlyList<TicketSummary>>.TryAsync(async () =>
		{
			_currentUser.RequireUserOid();
			if (string.IsNullOrWhiteSpace(typeId)
				&& string.IsNullOrWhiteSpace(categoryId)
				&& string.IsNullOrWhiteSpace(subcategoryId))
			{
				throw new TicketingValidationException("A category queue requires a type, category, or subcategory.");
			}

			var tickets = new List<TicketSummary>();
			await foreach (var ticket in _ticketQueryStore.GetByQueueAsync(typeId, categoryId, subcategoryId, status, pageSize, cancellationToken))
			{
				if (await _permissions.CanViewTicketSummaryAsync(ticket, cancellationToken))
				{
					tickets.Add(ticket);
				}
			}

			return tickets;
		});

	public Task<DomainResult<IReadOnlyList<TicketSummary>>> GetByTagAsync(
		string tag,
		TicketStatus? status = null,
		int? pageSize = null,
		CancellationToken cancellationToken = default) =>
		DomainResult<IReadOnlyList<TicketSummary>>.TryAsync(async () =>
		{
			ArgumentException.ThrowIfNullOrWhiteSpace(tag);
			_currentUser.RequireUserOid();

			var tickets = new List<TicketSummary>();
			await foreach (var ticket in _ticketQueryStore.GetByTagAsync(tag, status, pageSize, cancellationToken))
			{
				if (await _permissions.CanViewTicketSummaryAsync(ticket, cancellationToken))
				{
					tickets.Add(ticket);
				}
			}

			return tickets;
		});

	public Task<DomainResult<IReadOnlyList<TicketSummary>>> SearchAsync(
		TicketSearchCriteria criteria,
		CancellationToken cancellationToken = default) =>
		DomainResult<IReadOnlyList<TicketSummary>>.TryAsync(async () =>
		{
			ArgumentNullException.ThrowIfNull(criteria);

			var pageSize = NormalizePageSize(criteria.PageSize);
			var tickets = new List<TicketSummary>();
			var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

			await foreach (var ticket in GetSearchCandidatesAsync(criteria, pageSize, cancellationToken))
			{
				if (!seen.Add(ticket.TicketId)
					|| !MatchesSearchCriteria(ticket, criteria)
					|| !await _permissions.CanViewTicketSummaryAsync(ticket, cancellationToken))
				{
					continue;
				}

				tickets.Add(ticket);
				if (tickets.Count >= pageSize)
				{
					break;
				}
			}

			return tickets;
		});

	public Task<DomainResult<TicketRecord>> UpdateAsync(UpdateTicketCommand command, CancellationToken cancellationToken = default) =>
		DomainResult<TicketRecord>.TryAsync(async () =>
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
		});

	public Task<DomainResult<TicketNoteRecord>> AddNoteAsync(AddTicketNoteCommand command, CancellationToken cancellationToken = default) =>
		DomainResult<TicketNoteRecord>.TryAsync(async () =>
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
		});

	public Task<DomainResult<IReadOnlyList<TicketNoteRecord>>> GetNotesAsync(
		string ticketId,
		int? pageSize = null,
		CancellationToken cancellationToken = default) =>
		DomainResult<IReadOnlyList<TicketNoteRecord>>.TryAsync(async () =>
		{
			var ticket = await GetRequiredTicketAsync(ticketId, cancellationToken);
			await EnsureCanViewAsync(ticket, cancellationToken);
			var includeInternal = await _permissions.CanWorkTicketAsync(ticket, cancellationToken);

			return await _ticketNoteStore.GetForTicketAsync(ticketId, includeInternal, pageSize, cancellationToken)
				.ToReadOnlyListAsync(cancellationToken);
		});

	public Task<DomainResult<TicketAttachmentRecord>> UploadAttachmentAsync(
		UploadTicketAttachmentCommand command,
		CancellationToken cancellationToken = default) =>
		DomainResult<TicketAttachmentRecord>.TryAsync(async () =>
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
		});

	public Task<DomainResult<IReadOnlyList<TicketAttachmentRecord>>> GetAttachmentsAsync(
		string ticketId,
		int? pageSize = null,
		CancellationToken cancellationToken = default) =>
		DomainResult<IReadOnlyList<TicketAttachmentRecord>>.TryAsync(async () =>
		{
			var ticket = await GetRequiredTicketAsync(ticketId, cancellationToken);
			await EnsureCanViewAsync(ticket, cancellationToken);

			return await _ticketAttachmentStore.GetForTicketAsync(ticketId, includeDeleted: false, pageSize, cancellationToken)
				.ToReadOnlyListAsync(cancellationToken);
		});

	public Task<DomainResult<Stream>> OpenAttachmentAsync(
		string ticketId,
		string attachmentId,
		CancellationToken cancellationToken = default) =>
		DomainResult<Stream>.TryAsync(async () =>
		{
			var ticket = await GetRequiredTicketAsync(ticketId, cancellationToken);
			await EnsureCanViewAsync(ticket, cancellationToken);
			return await _ticketAttachmentStore.OpenReadAsync(ticketId, attachmentId, cancellationToken);
		});

	public Task<DomainResult> DeleteAttachmentAsync(
		string ticketId,
		string attachmentId,
		CancellationToken cancellationToken = default) =>
		DomainResult.TryAsync(async () =>
		{
			var ticket = await GetRequiredTicketAsync(ticketId, cancellationToken);
			if (!await _permissions.CanWorkTicketAsync(ticket, cancellationToken))
			{
				throw new TicketingForbiddenException("Only ticket workers can delete attachments.");
			}

			var userOid = await _currentUser.RequireUserOidAndSyncProfileAsync(cancellationToken);
			await _ticketAttachmentStore.SoftDeleteAsync(ticketId, attachmentId, userOid, cancellationToken);
		});

	public Task<DomainResult<IReadOnlyList<TicketAuditEventRecord>>> GetAuditAsync(
		string ticketId,
		int? pageSize = null,
		CancellationToken cancellationToken = default) =>
		DomainResult<IReadOnlyList<TicketAuditEventRecord>>.TryAsync(async () =>
		{
			var ticket = await GetRequiredTicketAsync(ticketId, cancellationToken);
			if (!await _permissions.CanWorkTicketAsync(ticket, cancellationToken))
			{
				throw new TicketingForbiddenException("Only ticket workers can view audit history.");
			}

			return await _ticketAuditStore.GetForTicketAsync(ticketId, pageSize, cancellationToken)
				.ToReadOnlyListAsync(cancellationToken);
		});

	public Task<DomainResult<TicketRecord>> AssignAsync(AssignTicketCommand command, CancellationToken cancellationToken = default) =>
		DomainResult<TicketRecord>.TryAsync(async () =>
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
		});

	public Task<DomainResult<TicketRecord>> AssignTeamAsync(AssignTicketTeamCommand command, CancellationToken cancellationToken = default) =>
		DomainResult<TicketRecord>.TryAsync(async () =>
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
		});

	public Task<DomainResult<TicketRecord>> SetStatusAsync(SetTicketStatusCommand command, CancellationToken cancellationToken = default) =>
		DomainResult<TicketRecord>.TryAsync(async () =>
		{
			var ticket = await GetRequiredTicketAsync(command.TicketId, cancellationToken);
			if (command.Status == TicketStatus.Closed)
			{
				throw new TicketingValidationException("Use the close workflow to close tickets so a closure timestamp and resolution note are captured.");
			}

			var userOid = _currentUser.RequireUserOid();
			var canWork = await _permissions.CanWorkTicketAsync(ticket, cancellationToken);
			var isSubmitterCancellation = command.Status == TicketStatus.Cancelled
				&& string.Equals(ticket.SubmitterOid, userOid, StringComparison.OrdinalIgnoreCase);

			if (!canWork && !isSubmitterCancellation)
			{
				throw new TicketingForbiddenException("You do not have permission to change this ticket's status.");
			}

			var actorOid = await _currentUser.RequireUserOidAndSyncProfileAsync(cancellationToken);
			return await _ticketStore.SetStatusAsync(
				new SetTicketStatusRequest
				{
					TicketId = command.TicketId,
					ChangedByOid = actorOid,
					Status = command.Status,
					Reason = command.Reason,
					ExpectedETag = command.ExpectedETag
				},
				cancellationToken);
		});

	public Task<DomainResult<TicketRecord>> CloseAsync(CloseTicketCommand command, CancellationToken cancellationToken = default) =>
		DomainResult<TicketRecord>.TryAsync(async () =>
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
		});

	public Task<DomainResult<TicketRecord>> ReopenAsync(ReopenTicketCommand command, CancellationToken cancellationToken = default) =>
		DomainResult<TicketRecord>.TryAsync(async () =>
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
		});

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

	private void EnsureCanViewAllTickets(string message)
	{
		_currentUser.RequireUserOid();
		if (!_permissions.CanViewAllTickets())
		{
			throw new TicketingForbiddenException(message);
		}
	}

	private async Task<bool> IsOnTeamAsync(string userOid, string teamId, CancellationToken cancellationToken)
	{
		return await _teamStore.IsUserOnTeamAsync(userOid, teamId, cancellationToken);
	}

	private async IAsyncEnumerable<TicketSummary> GetSearchCandidatesAsync(
		TicketSearchCriteria criteria,
		int pageSize,
		[EnumeratorCancellation] CancellationToken cancellationToken)
	{
		var userOid = _currentUser.RequireUserOid();

		if (!string.IsNullOrWhiteSpace(criteria.Query))
		{
			var exactTicket = await _ticketStore.GetByNumberAsync(criteria.Query, cancellationToken);
			if (exactTicket is not null)
			{
				yield return ToSummary(exactTicket);
				yield break;
			}
		}

		if (!string.IsNullOrWhiteSpace(criteria.AssigneeOid))
		{
			if (!_permissions.CanViewAllTickets()
				&& !string.Equals(criteria.AssigneeOid, userOid, StringComparison.OrdinalIgnoreCase))
			{
				throw new TicketingForbiddenException("Only managers and admins can search another user's assigned tickets.");
			}

			await foreach (var ticket in _ticketQueryStore.GetAssignedAsync(criteria.AssigneeOid, criteria.Status, pageSize, cancellationToken))
			{
				yield return ticket;
			}

			yield break;
		}

		if (!string.IsNullOrWhiteSpace(criteria.SubmitterOid))
		{
			if (!_permissions.CanViewAllTickets()
				&& !string.Equals(criteria.SubmitterOid, userOid, StringComparison.OrdinalIgnoreCase))
			{
				throw new TicketingForbiddenException("Only managers and admins can search another user's submitted tickets.");
			}

			await foreach (var ticket in _ticketQueryStore.GetSubmittedAsync(criteria.SubmitterOid, criteria.Status, pageSize, cancellationToken))
			{
				yield return ticket;
			}

			yield break;
		}

		if (!string.IsNullOrWhiteSpace(criteria.AssignedTeamId))
		{
			if (!_permissions.CanManageTeams()
				&& (!_permissions.IsTechnicianOrAbove()
					|| !await IsOnTeamAsync(userOid, criteria.AssignedTeamId, cancellationToken)))
			{
				throw new TicketingForbiddenException("You do not have access to this team queue.");
			}

			await foreach (var ticket in _ticketQueryStore.GetByTeamAsync(criteria.AssignedTeamId, criteria.Status, pageSize, cancellationToken))
			{
				yield return ticket;
			}

			yield break;
		}

		if (!string.IsNullOrWhiteSpace(criteria.Tag))
		{
			await foreach (var ticket in _ticketQueryStore.GetByTagAsync(criteria.Tag, criteria.Status, pageSize, cancellationToken))
			{
				yield return ticket;
			}

			yield break;
		}

		if (!string.IsNullOrWhiteSpace(criteria.TypeId)
			|| !string.IsNullOrWhiteSpace(criteria.CategoryId)
			|| !string.IsNullOrWhiteSpace(criteria.SubcategoryId))
		{
			await foreach (var ticket in _ticketQueryStore.GetByQueueAsync(
				criteria.TypeId,
				criteria.CategoryId,
				criteria.SubcategoryId,
				criteria.Status,
				pageSize,
				cancellationToken))
			{
				yield return ticket;
			}

			yield break;
		}

		if (_permissions.CanViewAllTickets())
		{
			foreach (var status in Statuses(criteria.Status))
			{
				await foreach (var ticket in _ticketQueryStore.GetByStatusAsync(status, pageSize, cancellationToken))
				{
					yield return ticket;
				}
			}

			yield break;
		}

		await foreach (var ticket in _ticketQueryStore.GetSubmittedAsync(userOid, criteria.Status, pageSize, cancellationToken))
		{
			yield return ticket;
		}

		if (!_permissions.IsTechnicianOrAbove())
		{
			yield break;
		}

		await foreach (var ticket in _ticketQueryStore.GetAssignedAsync(userOid, criteria.Status, pageSize, cancellationToken))
		{
			yield return ticket;
		}

		await foreach (var membership in _teamStore.GetMembershipsForUserAsync(userOid, false, null, cancellationToken))
		{
			await foreach (var ticket in _ticketQueryStore.GetByTeamAsync(membership.TeamId, criteria.Status, pageSize, cancellationToken))
			{
				yield return ticket;
			}
		}
	}

	private static IEnumerable<TicketStatus> Statuses(TicketStatus? status) =>
		status.HasValue ? [status.Value] : SearchStatuses;

	private static bool MatchesSearchCriteria(TicketSummary ticket, TicketSearchCriteria criteria)
	{
		if (criteria.Status.HasValue && ticket.Status != criteria.Status.Value)
		{
			return false;
		}

		if (criteria.Priority.HasValue && ticket.Priority != criteria.Priority.Value)
		{
			return false;
		}

		if (!Matches(ticket.SubmitterOid, criteria.SubmitterOid)
			|| !Matches(ticket.AssigneeOid, criteria.AssigneeOid)
			|| !Matches(ticket.AssignedTeamId, criteria.AssignedTeamId)
			|| !Matches(ticket.TypeId, criteria.TypeId)
			|| !Matches(ticket.CategoryId, criteria.CategoryId)
			|| !Matches(ticket.SubcategoryId, criteria.SubcategoryId))
		{
			return false;
		}

		if (!string.IsNullOrWhiteSpace(criteria.Tag)
			&& !ticket.Tags.Contains(criteria.Tag, StringComparer.OrdinalIgnoreCase))
		{
			return false;
		}

		if (criteria.OpenedFromUtc.HasValue && ticket.OpenedUtc < criteria.OpenedFromUtc.Value)
		{
			return false;
		}

		if (criteria.OpenedToUtc.HasValue && ticket.OpenedUtc > criteria.OpenedToUtc.Value)
		{
			return false;
		}

		if (criteria.ClosedFromUtc.HasValue && (!ticket.ClosedUtc.HasValue || ticket.ClosedUtc.Value < criteria.ClosedFromUtc.Value))
		{
			return false;
		}

		if (criteria.ClosedToUtc.HasValue && (!ticket.ClosedUtc.HasValue || ticket.ClosedUtc.Value > criteria.ClosedToUtc.Value))
		{
			return false;
		}

		return string.IsNullOrWhiteSpace(criteria.Query)
			|| Contains(ticket.TicketNumber, criteria.Query)
			|| Contains(ticket.Title, criteria.Query);
	}

	private static bool Matches(string? actual, string? expected) =>
		string.IsNullOrWhiteSpace(expected) || string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);

	private static bool Contains(string? actual, string expected) =>
		actual?.Contains(expected, StringComparison.OrdinalIgnoreCase) == true;

	private static int NormalizePageSize(int? pageSize) =>
		Math.Clamp(pageSize.GetValueOrDefault(DefaultPageSize), 1, MaxPageSize);

	private static TicketSummary ToSummary(TicketRecord ticket) =>
		new()
		{
			TicketId = ticket.TicketId,
			TicketNumber = ticket.TicketNumber,
			Title = ticket.Title,
			Status = ticket.Status,
			Priority = ticket.Priority,
			TypeId = ticket.TypeId,
			CategoryId = ticket.CategoryId,
			SubcategoryId = ticket.SubcategoryId,
			SubmitterOid = ticket.SubmitterOid,
			AssigneeOid = ticket.AssigneeOid,
			AssignedTeamId = ticket.AssignedTeamId,
			OpenedUtc = ticket.OpenedUtc,
			ClosedUtc = ticket.ClosedUtc,
			LastUpdatedUtc = ticket.LastUpdatedUtc,
			Tags = ticket.Tags
		};
}
