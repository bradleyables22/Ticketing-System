using Microsoft.Extensions.Logging;
using Ticketing.Data.Models;
using Ticketing.Data.Stores;
using Ticketing.Domain.Configuration;

namespace Ticketing.Domain.Services;

internal sealed class TicketEmailNotificationService
{
	private const string SubmitterRole = "submitter";
	private const string AssigneeRole = "assignee";
	private const string PreviousAssigneeRole = "previousAssignee";
	private const string TeamMemberRoleName = "teamMember";
	private const string TeamLeadRoleName = "teamLead";
	private const string PreviousTeamMemberRole = "previousTeamMember";
	private const string PreviousTeamLeadRole = "previousTeamLead";

	private readonly TicketEmailNotificationOptions _options;
	private readonly ITicketEmailNotificationQueue _queue;
	private readonly IUserProfileStore _userProfiles;
	private readonly ITeamStore _teamStore;
	private readonly CurrentUserService _currentUser;
	private readonly ILogger<TicketEmailNotificationService> _logger;

	public TicketEmailNotificationService(
		TicketEmailNotificationOptions options,
		ITicketEmailNotificationQueue queue,
		IUserProfileStore userProfiles,
		ITeamStore teamStore,
		CurrentUserService currentUser,
		ILogger<TicketEmailNotificationService> logger)
	{
		_options = options;
		_queue = queue;
		_userProfiles = userProfiles;
		_teamStore = teamStore;
		_currentUser = currentUser;
		_logger = logger;
	}

	public Task TicketCreatedAsync(TicketRecord ticket, CancellationToken cancellationToken) =>
		EnqueueAsync(
			_options.Events.TicketCreated,
			"ticket.created",
			"ticket.created",
			ticket,
			new Dictionary<string, string?>(),
			async (recipients, actorOid) =>
			{
				await AddParticipantRecipientsAsync(recipients, ticket, actorOid, includeSubmitter: true, includeAssignee: true, includeTeam: true, cancellationToken);
			},
			cancellationToken);

	public Task TicketUpdatedAsync(TicketRecord ticket, CancellationToken cancellationToken) =>
		EnqueueAsync(
			_options.Events.TicketUpdated,
			"ticket.updated",
			"ticket.updated",
			ticket,
			new Dictionary<string, string?>(),
			async (recipients, actorOid) =>
			{
				await AddParticipantRecipientsAsync(recipients, ticket, actorOid, includeSubmitter: true, includeAssignee: true, includeTeam: true, cancellationToken);
			},
			cancellationToken);

	public Task TicketAssignedAsync(
		TicketRecord ticket,
		string? previousAssigneeOid,
		CancellationToken cancellationToken) =>
		EnqueueAsync(
			_options.Events.TicketAssigned,
			"ticket.assigned",
			"ticket.assigned",
			ticket,
			new Dictionary<string, string?>
			{
				["previousAssigneeOid"] = NormalizeOptional(previousAssigneeOid),
				["assigneeOid"] = NormalizeOptional(ticket.AssigneeOid)
			},
			async (recipients, actorOid) =>
			{
				await AddParticipantRecipientsAsync(recipients, ticket, actorOid, includeSubmitter: true, includeAssignee: true, includeTeam: false, cancellationToken);
				await AddUserRecipientAsync(recipients, previousAssigneeOid, PreviousAssigneeRole, actorOid, cancellationToken);
			},
			cancellationToken);

	public Task TeamAssignedAsync(
		TicketRecord ticket,
		string? previousTeamId,
		CancellationToken cancellationToken) =>
		EnqueueAsync(
			_options.Events.TeamAssigned,
			"ticket.team-assigned",
			"ticket.team-assigned",
			ticket,
			new Dictionary<string, string?>
			{
				["previousTeamId"] = NormalizeOptional(previousTeamId),
				["assignedTeamId"] = NormalizeOptional(ticket.AssignedTeamId)
			},
			async (recipients, actorOid) =>
			{
				await AddParticipantRecipientsAsync(recipients, ticket, actorOid, includeSubmitter: true, includeAssignee: true, includeTeam: true, cancellationToken);
				await AddTeamRecipientsAsync(recipients, previousTeamId, PreviousTeamMemberRole, PreviousTeamLeadRole, actorOid, cancellationToken);
			},
			cancellationToken);

	public Task StatusChangedAsync(
		TicketRecord ticket,
		TicketStatus previousStatus,
		string? reason,
		CancellationToken cancellationToken)
	{
		var eventName = ticket.Status switch
		{
			TicketStatus.Cancelled => "ticket.cancelled",
			TicketStatus.Resolved => "ticket.resolved",
			_ => "ticket.status-changed"
		};

		return EnqueueAsync(
			_options.Events.StatusChanged,
			eventName,
			eventName,
			ticket,
			new Dictionary<string, string?>
			{
				["previousStatus"] = previousStatus.ToString(),
				["status"] = ticket.Status.ToString(),
				["reason"] = NormalizeOptional(reason)
			},
			async (recipients, actorOid) =>
			{
				await AddParticipantRecipientsAsync(recipients, ticket, actorOid, includeSubmitter: true, includeAssignee: true, includeTeam: true, cancellationToken);
			},
			cancellationToken);
	}

	public Task TicketClosedAsync(
		TicketRecord ticket,
		string? resolutionNote,
		CancellationToken cancellationToken) =>
		EnqueueAsync(
			_options.Events.TicketClosed,
			"ticket.closed",
			"ticket.closed",
			ticket,
			new Dictionary<string, string?>
			{
				["resolutionNote"] = NormalizeOptional(resolutionNote)
			},
			async (recipients, actorOid) =>
			{
				await AddParticipantRecipientsAsync(recipients, ticket, actorOid, includeSubmitter: true, includeAssignee: true, includeTeam: true, cancellationToken);
			},
			cancellationToken);

	public Task TicketReopenedAsync(
		TicketRecord ticket,
		string? reason,
		CancellationToken cancellationToken) =>
		EnqueueAsync(
			_options.Events.TicketReopened,
			"ticket.reopened",
			"ticket.reopened",
			ticket,
			new Dictionary<string, string?>
			{
				["reason"] = NormalizeOptional(reason)
			},
			async (recipients, actorOid) =>
			{
				await AddParticipantRecipientsAsync(recipients, ticket, actorOid, includeSubmitter: true, includeAssignee: true, includeTeam: true, cancellationToken);
			},
			cancellationToken);

	public Task NoteAddedAsync(
		TicketRecord ticket,
		TicketNoteRecord note,
		CancellationToken cancellationToken)
	{
		var isInternal = note.IsInternal;
		return EnqueueAsync(
			isInternal ? _options.Events.InternalNoteAdded : _options.Events.PublicNoteAdded,
			isInternal ? "ticket.internal-note-added" : "ticket.note-added",
			isInternal ? "ticket.internal-note-added" : "ticket.note-added",
			ticket,
			new Dictionary<string, string?>
			{
				["noteId"] = note.NoteId,
				["isInternal"] = note.IsInternal.ToString()
			},
			async (recipients, actorOid) =>
			{
				await AddParticipantRecipientsAsync(
					recipients,
					ticket,
					actorOid,
					includeSubmitter: !isInternal,
					includeAssignee: true,
					includeTeam: true,
					cancellationToken);
			},
			cancellationToken);
	}

	public Task AttachmentAddedAsync(
		TicketRecord ticket,
		TicketAttachmentRecord attachment,
		CancellationToken cancellationToken) =>
		EnqueueAsync(
			_options.Events.AttachmentAdded,
			"ticket.attachment-added",
			"ticket.attachment-added",
			ticket,
			new Dictionary<string, string?>
			{
				["attachmentId"] = attachment.AttachmentId,
				["fileName"] = attachment.OriginalFileName,
				["contentType"] = attachment.ContentType,
				["sizeBytes"] = attachment.SizeBytes.ToString()
			},
			async (recipients, actorOid) =>
			{
				await AddParticipantRecipientsAsync(recipients, ticket, actorOid, includeSubmitter: true, includeAssignee: true, includeTeam: true, cancellationToken);
			},
			cancellationToken);

	public Task AttachmentDeletedAsync(
		TicketRecord ticket,
		string attachmentId,
		CancellationToken cancellationToken) =>
		EnqueueAsync(
			_options.Events.AttachmentDeleted,
			"ticket.attachment-deleted",
			"ticket.attachment-deleted",
			ticket,
			new Dictionary<string, string?>
			{
				["attachmentId"] = attachmentId
			},
			async (recipients, actorOid) =>
			{
				await AddParticipantRecipientsAsync(recipients, ticket, actorOid, includeSubmitter: true, includeAssignee: true, includeTeam: true, cancellationToken);
			},
			cancellationToken);

	private async Task EnqueueAsync(
		bool eventEnabled,
		string eventName,
		string templateKey,
		TicketRecord ticket,
		IReadOnlyDictionary<string, string?> data,
		Func<Dictionary<string, RecipientBuilder>, string, Task> configureRecipients,
		CancellationToken cancellationToken)
	{
		if (!_options.Enabled || !eventEnabled)
		{
			return;
		}

		try
		{
			var actorOid = _currentUser.RequireUserOid();
			var recipients = new Dictionary<string, RecipientBuilder>(StringComparer.OrdinalIgnoreCase);
			await configureRecipients(recipients, actorOid);

			var recipientRecords = recipients.Values
				.Where(recipient => !_options.ExcludeActorFromRecipients
					|| !recipient.UserOid.Equals(actorOid, StringComparison.OrdinalIgnoreCase))
				.Select(recipient => recipient.ToRecord())
				.ToArray();

			if (recipientRecords.Length == 0)
			{
				return;
			}

			await _queue.EnqueueAsync(
				new QueueTicketEmailNotificationRequest
				{
					EventName = eventName,
					TemplateKey = templateKey,
					Ticket = ToNotificationTicket(ticket),
					Actor = await GetActorAsync(actorOid, cancellationToken),
					Recipients = recipientRecords,
					Data = data
				},
				cancellationToken);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			_logger.LogWarning(
				ex,
				"Failed to enqueue ticket email notification {EventName} for ticket {TicketId}.",
				eventName,
				ticket.TicketId);
		}
	}

	private async Task AddParticipantRecipientsAsync(
		Dictionary<string, RecipientBuilder> recipients,
		TicketRecord ticket,
		string actorOid,
		bool includeSubmitter,
		bool includeAssignee,
		bool includeTeam,
		CancellationToken cancellationToken)
	{
		if (includeSubmitter)
		{
			await AddUserRecipientAsync(recipients, ticket.SubmitterOid, SubmitterRole, actorOid, cancellationToken);
		}

		if (includeAssignee)
		{
			await AddUserRecipientAsync(recipients, ticket.AssigneeOid, AssigneeRole, actorOid, cancellationToken);
		}

		if (includeTeam)
		{
			await AddTeamRecipientsAsync(recipients, ticket.AssignedTeamId, TeamMemberRoleName, TeamLeadRoleName, actorOid, cancellationToken);
		}
	}

	private async Task AddTeamRecipientsAsync(
		Dictionary<string, RecipientBuilder> recipients,
		string? teamId,
		string memberRole,
		string leadRole,
		string actorOid,
		CancellationToken cancellationToken)
	{
		if (string.IsNullOrWhiteSpace(teamId) || _options.MaxTeamRecipients <= 0)
		{
			return;
		}

		var added = 0;
		await foreach (var member in _teamStore.GetMembersAsync(teamId, includeInactive: false, pageSize: _options.MaxTeamRecipients, cancellationToken))
		{
			await AddUserRecipientAsync(
				recipients,
				member.UserOid,
				member.Role == TeamMemberRole.Lead ? leadRole : memberRole,
				actorOid,
				cancellationToken);

			added++;
			if (added >= _options.MaxTeamRecipients)
			{
				break;
			}
		}
	}

	private async Task AddUserRecipientAsync(
		Dictionary<string, RecipientBuilder> recipients,
		string? userOid,
		string role,
		string actorOid,
		CancellationToken cancellationToken)
	{
		if (string.IsNullOrWhiteSpace(userOid))
		{
			return;
		}

		var normalizedUserOid = userOid.Trim();
		if (_options.ExcludeActorFromRecipients
			&& normalizedUserOid.Equals(actorOid, StringComparison.OrdinalIgnoreCase))
		{
			return;
		}

		if (!recipients.TryGetValue(normalizedUserOid, out var recipient))
		{
			var profile = await _userProfiles.GetAsync(normalizedUserOid, cancellationToken);
			recipient = new RecipientBuilder(
				normalizedUserOid,
				profile?.DisplayName,
				profile?.Email);
			recipients.Add(normalizedUserOid, recipient);
		}

		recipient.AddRole(role);
	}

	private async Task<TicketEmailNotificationActor> GetActorAsync(
		string actorOid,
		CancellationToken cancellationToken)
	{
		var profile = await _userProfiles.GetAsync(actorOid, cancellationToken);
		return new TicketEmailNotificationActor
		{
			UserOid = actorOid,
			DisplayName = profile?.DisplayName ?? _currentUser.Current.DisplayName,
			Email = profile?.Email ?? _currentUser.Current.Email
		};
	}

	private TicketEmailNotificationTicket ToNotificationTicket(TicketRecord ticket) =>
		new()
		{
			TicketId = ticket.TicketId,
			TicketNumber = ticket.TicketNumber,
			Title = ticket.Title,
			Description = _options.IncludeTicketDescription ? ticket.Description : null,
			Status = ticket.Status,
			Priority = ticket.Priority,
			TypeId = ticket.TypeId,
			CategoryId = ticket.CategoryId,
			SubcategoryId = ticket.SubcategoryId,
			SubmitterOid = ticket.SubmitterOid,
			CreatedByOid = ticket.CreatedByOid,
			AssigneeOid = ticket.AssigneeOid,
			AssignedTeamId = ticket.AssignedTeamId,
			OpenedUtc = ticket.OpenedUtc,
			ClosedUtc = ticket.ClosedUtc,
			LastUpdatedUtc = ticket.LastUpdatedUtc
		};

	private static string? NormalizeOptional(string? value) =>
		string.IsNullOrWhiteSpace(value) ? null : value.Trim();

	private sealed class RecipientBuilder
	{
		private readonly HashSet<string> _roles = new(StringComparer.OrdinalIgnoreCase);

		public RecipientBuilder(string userOid, string? displayName, string? email)
		{
			UserOid = userOid;
			DisplayName = displayName;
			Email = email;
		}

		public string UserOid { get; }

		public string? DisplayName { get; }

		public string? Email { get; }

		public void AddRole(string role)
		{
			if (!string.IsNullOrWhiteSpace(role))
			{
				_roles.Add(role.Trim());
			}
		}

		public TicketEmailNotificationRecipient ToRecord() =>
			new()
			{
				UserOid = UserOid,
				DisplayName = DisplayName,
				Email = Email,
				Roles = _roles.Order(StringComparer.OrdinalIgnoreCase).ToArray()
			};
	}
}
