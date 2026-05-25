using Azure.Data.Tables;
using Ticketing.Data.AzureStorage.Entities;
using Ticketing.Data.AzureStorage.Internal;
using Ticketing.Data.Models;
using Ticketing.Data.Stores;

namespace Ticketing.Data.AzureStorage;

internal sealed class AzureTicketStore : ITicketStore
{
	private readonly AzureStorageClients _clients;
	private readonly TicketIndexProjector _projector;
	private readonly TicketAuditWriter _auditWriter;
	private readonly TicketMutationService _mutations;
	private readonly ITeamStore _teamStore;

	public AzureTicketStore(
		AzureStorageClients clients,
		TicketIndexProjector projector,
		TicketAuditWriter auditWriter,
		TicketMutationService mutations,
		ITeamStore teamStore)
	{
		_clients = clients;
		_projector = projector;
		_auditWriter = auditWriter;
		_mutations = mutations;
		_teamStore = teamStore;
	}

	public async Task<TicketRecord> CreateAsync(CreateTicketRequest request, CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(request.Title);
		ArgumentException.ThrowIfNullOrWhiteSpace(request.Description);
		ArgumentException.ThrowIfNullOrWhiteSpace(request.SubmitterOid);

		var openedUtc = DateTimeOffset.UtcNow;
		var assignedTeamId = NormalizeOptional(request.AssignedTeamId);
		if (assignedTeamId is null)
		{
			var resolution = await _teamStore.ResolveTeamAsync(
				request.TypeId,
				request.CategoryId,
				request.SubcategoryId,
				request.Priority,
				cancellationToken);

			assignedTeamId = resolution?.TeamId;
		}

		var entity = TicketEntity.FromCreateRequest(request, openedUtc, assignedTeamId);

		await _clients.Tickets.AddEntityAsync(entity, cancellationToken);

		var lookup = new TicketLookupEntity
		{
			PartitionKey = StorageKeys.TicketLookupPartition(),
			RowKey = StorageKeys.TicketLookupRow(entity.TicketNumber),
			TicketId = entity.TicketId,
			TicketNumber = entity.TicketNumber,
			TicketPartitionKey = entity.PartitionKey,
			TicketRowKey = entity.RowKey
		};

		await _clients.TicketLookups.AddEntityAsync(lookup, cancellationToken);
		await _projector.UpsertAsync(entity, cancellationToken);
		await _auditWriter.AppendAsync(
			entity.TicketId,
			entity.SubmitterOid,
			TicketAuditEventType.TicketCreated,
			null,
			null,
			entity.TicketNumber,
			"Ticket created.",
			cancellationToken);

		return entity.ToRecord();
	}

	public async Task<TicketRecord?> GetAsync(string ticketId, CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(ticketId);

		var response = await _clients.Tickets.GetEntityIfExistsAsync<TicketEntity>(
			StorageKeys.TicketPartition(ticketId),
			StorageKeys.TicketRow(ticketId),
			cancellationToken: cancellationToken);

		return response.HasValue ? response.Value!.ToRecord() : null;
	}

	public async Task<TicketRecord?> GetByNumberAsync(string ticketNumber, CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(ticketNumber);

		var lookup = await _clients.TicketLookups.GetEntityIfExistsAsync<TicketLookupEntity>(
			StorageKeys.TicketLookupPartition(),
			StorageKeys.TicketLookupRow(ticketNumber),
			cancellationToken: cancellationToken);

		if (!lookup.HasValue)
		{
			return null;
		}

		var ticket = await _clients.Tickets.GetEntityIfExistsAsync<TicketEntity>(
			lookup.Value!.TicketPartitionKey,
			lookup.Value!.TicketRowKey,
			cancellationToken: cancellationToken);

		return ticket.HasValue ? ticket.Value!.ToRecord() : null;
	}

	public async Task<TicketRecord> UpdateDetailsAsync(UpdateTicketDetailsRequest request, CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(request.TicketId);
		ArgumentException.ThrowIfNullOrWhiteSpace(request.UpdatedByOid);

		var updated = await _mutations.MutateAsync(
			request.TicketId,
			request.ExpectedETag,
			ticket =>
			{
				if (!string.IsNullOrWhiteSpace(request.Title))
				{
					ticket.Title = request.Title.Trim();
				}

				if (request.Description is not null)
				{
					ticket.Description = request.Description;
				}

				if (request.Priority.HasValue)
				{
					ticket.Priority = (int)request.Priority.Value;
				}

				if (request.ClearClassification)
				{
					ticket.TypeId = null;
					ticket.CategoryId = null;
					ticket.SubcategoryId = null;
				}
				else
				{
					ticket.TypeId = NormalizeOptional(request.TypeId) ?? ticket.TypeId;
					ticket.CategoryId = NormalizeOptional(request.CategoryId) ?? ticket.CategoryId;
					ticket.SubcategoryId = NormalizeOptional(request.SubcategoryId) ?? ticket.SubcategoryId;
				}

				if (request.Tags is not null)
				{
					ticket.TagsJson = StorageKeys.SerializeTags(request.Tags);
				}

				ticket.LastUpdatedUtc = DateTimeOffset.UtcNow;
			},
			request.UpdatedByOid,
			TicketAuditEventType.DetailsChanged,
			null,
			null,
			null,
			"Ticket details updated.",
			cancellationToken);

		return updated.ToRecord();
	}

	public async Task<TicketRecord> AssignAsync(AssignTicketRequest request, CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(request.TicketId);
		ArgumentException.ThrowIfNullOrWhiteSpace(request.ChangedByOid);

		var newAssignee = NormalizeOptional(request.AssigneeOid);

		var updated = await _mutations.MutateAsync(
			request.TicketId,
			request.ExpectedETag,
			ticket =>
			{
				ticket.AssigneeOid = newAssignee;
				if (ticket.Status == TicketStatus.Open.ToString() && newAssignee is not null)
				{
					ticket.Status = TicketStatus.InProgress.ToString();
				}

				ticket.LastUpdatedUtc = DateTimeOffset.UtcNow;
			},
			request.ChangedByOid,
			TicketAuditEventType.Assigned,
			nameof(TicketRecord.AssigneeOid),
			null,
			newAssignee,
			request.Reason ?? "Ticket assignment changed.",
			cancellationToken);

		return updated.ToRecord();
	}

	public async Task<TicketRecord> AssignTeamAsync(AssignTicketTeamRequest request, CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(request.TicketId);
		ArgumentException.ThrowIfNullOrWhiteSpace(request.ChangedByOid);

		var newTeamId = NormalizeOptional(request.AssignedTeamId);
		if (newTeamId is not null && await _teamStore.GetTeamAsync(newTeamId, cancellationToken) is null)
		{
			throw new KeyNotFoundException($"Team '{newTeamId}' was not found.");
		}

		var updated = await _mutations.MutateAsync(
			request.TicketId,
			request.ExpectedETag,
			ticket =>
			{
				ticket.AssignedTeamId = newTeamId;
				ticket.LastUpdatedUtc = DateTimeOffset.UtcNow;
			},
			request.ChangedByOid,
			TicketAuditEventType.TeamAssigned,
			nameof(TicketRecord.AssignedTeamId),
			null,
			newTeamId,
			request.Reason ?? "Ticket team assignment changed.",
			cancellationToken);

		return updated.ToRecord();
	}

	public async Task<TicketRecord> CloseAsync(CloseTicketRequest request, CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(request.TicketId);
		ArgumentException.ThrowIfNullOrWhiteSpace(request.ClosedByOid);

		var closedUtc = DateTimeOffset.UtcNow;
		var updated = await _mutations.MutateAsync(
			request.TicketId,
			request.ExpectedETag,
			ticket =>
			{
				ticket.Status = TicketStatus.Closed.ToString();
				ticket.ClosedUtc = closedUtc;
				ticket.LastUpdatedUtc = closedUtc;
			},
			request.ClosedByOid,
			TicketAuditEventType.Closed,
			nameof(TicketRecord.Status),
			null,
			TicketStatus.Closed.ToString(),
			request.ResolutionNote ?? "Ticket closed.",
			cancellationToken);

		return updated.ToRecord();
	}

	public async Task<TicketRecord> ReopenAsync(ReopenTicketRequest request, CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(request.TicketId);
		ArgumentException.ThrowIfNullOrWhiteSpace(request.ReopenedByOid);

		var updated = await _mutations.MutateAsync(
			request.TicketId,
			request.ExpectedETag,
			ticket =>
			{
				ticket.Status = string.IsNullOrWhiteSpace(ticket.AssigneeOid)
					? TicketStatus.Open.ToString()
					: TicketStatus.InProgress.ToString();
				ticket.ClosedUtc = null;
				ticket.LastUpdatedUtc = DateTimeOffset.UtcNow;
			},
			request.ReopenedByOid,
			TicketAuditEventType.Reopened,
			nameof(TicketRecord.Status),
			TicketStatus.Closed.ToString(),
			null,
			request.Reason ?? "Ticket reopened.",
			cancellationToken);

		return updated.ToRecord();
	}

	private static string? NormalizeOptional(string? value) =>
		string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
