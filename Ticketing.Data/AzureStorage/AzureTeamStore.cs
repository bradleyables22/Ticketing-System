using System.Runtime.CompilerServices;
using Azure;
using Azure.Data.Tables;
using Ticketing.Data.AzureStorage.Entities;
using Ticketing.Data.AzureStorage.Internal;
using Ticketing.Data.Models;
using Ticketing.Data.Stores;

namespace Ticketing.Data.AzureStorage;

internal sealed class AzureTeamStore : ITeamStore
{
	private readonly AzureStorageClients _clients;

	public AzureTeamStore(AzureStorageClients clients)
	{
		_clients = clients;
	}

	public async Task<TeamRecord> SaveTeamAsync(SaveTeamRequest request, CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(request.Name);
		ArgumentException.ThrowIfNullOrWhiteSpace(request.ActorOid);

		var teamId = NormalizeOptional(request.TeamId) ?? StorageKeys.NewId();
		var entity = await GetOrCreateTeamEntityAsync(teamId, request.ActorOid, cancellationToken);

		entity.Name = request.Name.Trim();
		entity.Description = NormalizeOptional(request.Description);
		entity.IsActive = request.IsActive;
		entity.UpdatedByOid = request.ActorOid.Trim();
		entity.UpdatedUtc = DateTimeOffset.UtcNow;

		await _clients.Teams.UpsertEntityAsync(entity, TableUpdateMode.Replace, cancellationToken);
		return entity.ToRecord();
	}

	public async Task<TeamMemberRecord> SaveMemberAsync(SaveTeamMemberRequest request, CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(request.TeamId);
		ArgumentException.ThrowIfNullOrWhiteSpace(request.UserOid);
		ArgumentException.ThrowIfNullOrWhiteSpace(request.ActorOid);

		var team = await GetTeamAsync(request.TeamId, cancellationToken);
		if (team is null)
		{
			throw new KeyNotFoundException($"Team '{request.TeamId}' was not found.");
		}

		var existing = await _clients.TeamMembers.GetEntityIfExistsAsync<TeamMemberEntity>(
			StorageKeys.TeamMemberByTeamPartition(request.TeamId),
			StorageKeys.TeamMemberByTeamRow(request.UserOid),
			cancellationToken: cancellationToken);

		var now = DateTimeOffset.UtcNow;
		var entity = existing.HasValue
			? existing.Value!
			: new TeamMemberEntity
			{
				TeamId = request.TeamId.Trim(),
				UserOid = request.UserOid.Trim(),
				CreatedByOid = request.ActorOid.Trim(),
				CreatedUtc = now
			};

		entity.PartitionKey = StorageKeys.TeamMemberByTeamPartition(request.TeamId);
		entity.RowKey = StorageKeys.TeamMemberByTeamRow(request.UserOid);
		entity.TeamId = request.TeamId.Trim();
		entity.UserOid = request.UserOid.Trim();
		entity.Role = request.Role.ToString();
		entity.IsActive = request.IsActive;
		entity.UpdatedByOid = request.ActorOid.Trim();
		entity.UpdatedUtc = now;

		var userProjection = entity.CopyForUserProjection();
		userProjection.PartitionKey = StorageKeys.TeamMemberByUserPartition(request.UserOid);
		userProjection.RowKey = StorageKeys.TeamMemberByUserRow(request.TeamId);

		await _clients.TeamMembers.UpsertEntityAsync(entity, TableUpdateMode.Replace, cancellationToken);
		await _clients.TeamMembers.UpsertEntityAsync(userProjection, TableUpdateMode.Replace, cancellationToken);

		return entity.ToRecord();
	}

	public async Task<TeamCategoryAssignmentRecord> SaveCategoryAssignmentAsync(
		SaveTeamCategoryAssignmentRequest request,
		CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(request.TeamId);
		ArgumentException.ThrowIfNullOrWhiteSpace(request.ActorOid);
		ValidateRoutingRequest(request);

		var team = await GetTeamAsync(request.TeamId, cancellationToken);
		if (team is null)
		{
			throw new KeyNotFoundException($"Team '{request.TeamId}' was not found.");
		}

		var assignmentId = NormalizeOptional(request.AssignmentId) ?? StorageKeys.NewId();
		var existing = await DeleteExistingAssignmentRowsAsync(assignmentId, cancellationToken);
		var now = DateTimeOffset.UtcNow;

		var entity = new TeamCategoryAssignmentEntity
		{
			PartitionKey = GetRoutePartition(request),
			RowKey = StorageKeys.TeamRouteRow(assignmentId),
			AssignmentId = assignmentId,
			TeamId = request.TeamId.Trim(),
			TypeId = NormalizeOptional(request.TypeId),
			CategoryId = NormalizeOptional(request.CategoryId),
			SubcategoryId = NormalizeOptional(request.SubcategoryId),
			Priority = request.Priority.HasValue ? (int)request.Priority.Value : null,
			IsDefault = request.IsDefault,
			IsActive = request.IsActive,
			SortOrder = request.SortOrder,
			CreatedByOid = existing?.CreatedByOid ?? request.ActorOid.Trim(),
			CreatedUtc = existing?.CreatedUtc ?? now,
			UpdatedByOid = request.ActorOid.Trim(),
			UpdatedUtc = now
		};

		await _clients.TeamRouting.UpsertEntityAsync(entity, TableUpdateMode.Replace, cancellationToken);
		return entity.ToRecord();
	}

	public async Task<TeamRecord?> GetTeamAsync(string teamId, CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(teamId);

		var response = await _clients.Teams.GetEntityIfExistsAsync<TeamEntity>(
			StorageKeys.TeamDefinitionPartition(),
			StorageKeys.TeamDefinitionRow(teamId),
			cancellationToken: cancellationToken);

		return response.HasValue ? response.Value!.ToRecord() : null;
	}

	public async IAsyncEnumerable<TeamRecord> GetTeamsAsync(
		bool includeInactive = false,
		int? pageSize = null,
		[EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		var partitionKey = StorageKeys.TeamDefinitionPartition();
		var filter = TableClient.CreateQueryFilter($"PartitionKey eq {partitionKey}");
		var normalizedPageSize = AzureTablePageLimits.Normalize(pageSize);
		var returned = 0;

		await foreach (var entity in _clients.Teams
			.QueryAsync<TeamEntity>(filter, maxPerPage: normalizedPageSize, cancellationToken: cancellationToken)
			.ConfigureAwait(false))
		{
			if (includeInactive || entity.IsActive)
			{
				yield return entity.ToRecord();
				returned++;
				if (AzureTablePageLimits.IsFull(normalizedPageSize, returned))
				{
					yield break;
				}
			}
		}
	}

	public async IAsyncEnumerable<TeamMemberRecord> GetMembersAsync(
		string teamId,
		bool includeInactive = false,
		int? pageSize = null,
		[EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(teamId);

		var partitionKey = StorageKeys.TeamMemberByTeamPartition(teamId);
		var filter = TableClient.CreateQueryFilter($"PartitionKey eq {partitionKey}");
		var normalizedPageSize = AzureTablePageLimits.Normalize(pageSize);
		var returned = 0;

		await foreach (var entity in _clients.TeamMembers
			.QueryAsync<TeamMemberEntity>(filter, maxPerPage: normalizedPageSize, cancellationToken: cancellationToken)
			.ConfigureAwait(false))
		{
			if (includeInactive || entity.IsActive)
			{
				yield return entity.ToRecord();
				returned++;
				if (AzureTablePageLimits.IsFull(normalizedPageSize, returned))
				{
					yield break;
				}
			}
		}
	}

	public async IAsyncEnumerable<TeamMemberRecord> GetMembershipsForUserAsync(
		string userOid,
		bool includeInactive = false,
		int? pageSize = null,
		[EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(userOid);

		var partitionKey = StorageKeys.TeamMemberByUserPartition(userOid);
		var filter = TableClient.CreateQueryFilter($"PartitionKey eq {partitionKey}");
		var normalizedPageSize = AzureTablePageLimits.Normalize(pageSize);
		var returned = 0;

		await foreach (var entity in _clients.TeamMembers
			.QueryAsync<TeamMemberEntity>(filter, maxPerPage: normalizedPageSize, cancellationToken: cancellationToken)
			.ConfigureAwait(false))
		{
			if (includeInactive || entity.IsActive)
			{
				yield return entity.ToRecord();
				returned++;
				if (AzureTablePageLimits.IsFull(normalizedPageSize, returned))
				{
					yield break;
				}
			}
		}
	}

	public async IAsyncEnumerable<TeamCategoryAssignmentRecord> GetCategoryAssignmentsAsync(
		string? teamId = null,
		bool includeInactive = false,
		int? pageSize = null,
		[EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		var filter = NormalizeOptional(teamId) is { } normalizedTeamId
			? TableClient.CreateQueryFilter($"TeamId eq {normalizedTeamId}")
			: null;
		var normalizedPageSize = AzureTablePageLimits.Normalize(pageSize);
		var returned = 0;

		await foreach (var entity in _clients.TeamRouting
			.QueryAsync<TeamCategoryAssignmentEntity>(filter, maxPerPage: normalizedPageSize, cancellationToken: cancellationToken)
			.ConfigureAwait(false))
		{
			if (includeInactive || entity.IsActive)
			{
				yield return entity.ToRecord();
				returned++;
				if (AzureTablePageLimits.IsFull(normalizedPageSize, returned))
				{
					yield break;
				}
			}
		}
	}

	public async Task<TeamRouteResolution?> ResolveTeamAsync(
		string? typeId,
		string? categoryId,
		string? subcategoryId,
		TicketPriority? priority = null,
		CancellationToken cancellationToken = default)
	{
		foreach (var route in GetResolutionPartitions(typeId, categoryId, subcategoryId, priority))
		{
			var assignment = await GetBestActiveRouteAsync(route.PartitionKey, cancellationToken);
			if (assignment is not null)
			{
				return new TeamRouteResolution
				{
					TeamId = assignment.TeamId,
					AssignmentId = assignment.AssignmentId,
					MatchLevel = route.MatchLevel
				};
			}
		}

		return null;
	}

	public async Task<bool> IsUserOnTeamAsync(string userOid, string teamId, CancellationToken cancellationToken = default)
	{
		var membership = await GetMembershipEntityAsync(userOid, teamId, cancellationToken);
		return membership?.IsActive == true;
	}

	public async Task<bool> IsUserTeamLeadAsync(string userOid, string teamId, CancellationToken cancellationToken = default)
	{
		var membership = await GetMembershipEntityAsync(userOid, teamId, cancellationToken);
		return membership?.IsActive == true && Enum.Parse<TeamMemberRole>(membership.Role) == TeamMemberRole.Lead;
	}

	private async Task<TeamEntity> GetOrCreateTeamEntityAsync(
		string teamId,
		string actorOid,
		CancellationToken cancellationToken)
	{
		var response = await _clients.Teams.GetEntityIfExistsAsync<TeamEntity>(
			StorageKeys.TeamDefinitionPartition(),
			StorageKeys.TeamDefinitionRow(teamId),
			cancellationToken: cancellationToken);

		if (response.HasValue)
		{
			return response.Value!;
		}

		var now = DateTimeOffset.UtcNow;
		return new TeamEntity
		{
			PartitionKey = StorageKeys.TeamDefinitionPartition(),
			RowKey = StorageKeys.TeamDefinitionRow(teamId),
			TeamId = teamId,
			CreatedByOid = actorOid.Trim(),
			CreatedUtc = now
		};
	}

	private async Task<TeamCategoryAssignmentEntity?> DeleteExistingAssignmentRowsAsync(
		string assignmentId,
		CancellationToken cancellationToken)
	{
		TeamCategoryAssignmentEntity? existing = null;
		var filter = TableClient.CreateQueryFilter($"AssignmentId eq {assignmentId}");

		await foreach (var entity in _clients.TeamRouting
			.QueryAsync<TeamCategoryAssignmentEntity>(filter, cancellationToken: cancellationToken)
			.ConfigureAwait(false))
		{
			existing ??= entity;
			await _clients.TeamRouting.DeleteEntityAsync(entity.PartitionKey, entity.RowKey, ETag.All, cancellationToken);
		}

		return existing;
	}

	private async Task<TeamCategoryAssignmentEntity?> GetBestActiveRouteAsync(
		string partitionKey,
		CancellationToken cancellationToken)
	{
		var filter = TableClient.CreateQueryFilter($"PartitionKey eq {partitionKey}");
		var candidates = new List<TeamCategoryAssignmentEntity>();

		await foreach (var entity in _clients.TeamRouting
			.QueryAsync<TeamCategoryAssignmentEntity>(filter, cancellationToken: cancellationToken)
			.ConfigureAwait(false))
		{
			if (entity.IsActive)
			{
				candidates.Add(entity);
			}
		}

		foreach (var assignment in candidates.OrderBy(entity => entity.SortOrder).ThenBy(entity => entity.CreatedUtc))
		{
			var team = await GetTeamAsync(assignment.TeamId, cancellationToken);
			if (team?.IsActive == true)
			{
				return assignment;
			}
		}

		return null;
	}

	private async Task<TeamMemberEntity?> GetMembershipEntityAsync(
		string userOid,
		string teamId,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(userOid);
		ArgumentException.ThrowIfNullOrWhiteSpace(teamId);

		var response = await _clients.TeamMembers.GetEntityIfExistsAsync<TeamMemberEntity>(
			StorageKeys.TeamMemberByUserPartition(userOid),
			StorageKeys.TeamMemberByUserRow(teamId),
			cancellationToken: cancellationToken);

		return response.HasValue ? response.Value! : null;
	}

	private static IEnumerable<(string PartitionKey, string MatchLevel)> GetResolutionPartitions(
		string? typeId,
		string? categoryId,
		string? subcategoryId,
		TicketPriority? priority)
	{
		if (!string.IsNullOrWhiteSpace(subcategoryId))
		{
			foreach (var currentPriority in PriorityCandidates(priority))
			{
				yield return (StorageKeys.TeamRouteSubcategoryPartition(subcategoryId, currentPriority), "Subcategory");
			}
		}

		if (!string.IsNullOrWhiteSpace(categoryId))
		{
			foreach (var currentPriority in PriorityCandidates(priority))
			{
				yield return (StorageKeys.TeamRouteCategoryPartition(categoryId, currentPriority), "Category");
			}
		}

		if (!string.IsNullOrWhiteSpace(typeId))
		{
			foreach (var currentPriority in PriorityCandidates(priority))
			{
				yield return (StorageKeys.TeamRouteTypePartition(typeId, currentPriority), "Type");
			}
		}

		foreach (var currentPriority in PriorityCandidates(priority))
		{
			yield return (StorageKeys.TeamRouteDefaultPartition(currentPriority), "Default");
		}
	}

	private static IEnumerable<TicketPriority?> PriorityCandidates(TicketPriority? priority)
	{
		if (priority.HasValue)
		{
			yield return priority.Value;
		}

		yield return null;
	}

	private static string GetRoutePartition(SaveTeamCategoryAssignmentRequest request)
	{
		if (request.IsDefault)
		{
			return StorageKeys.TeamRouteDefaultPartition(request.Priority);
		}

		if (!string.IsNullOrWhiteSpace(request.SubcategoryId))
		{
			return StorageKeys.TeamRouteSubcategoryPartition(request.SubcategoryId, request.Priority);
		}

		if (!string.IsNullOrWhiteSpace(request.CategoryId))
		{
			return StorageKeys.TeamRouteCategoryPartition(request.CategoryId, request.Priority);
		}

		return StorageKeys.TeamRouteTypePartition(request.TypeId!, request.Priority);
	}

	private static void ValidateRoutingRequest(SaveTeamCategoryAssignmentRequest request)
	{
		if (request.IsDefault)
		{
			return;
		}

		if (string.IsNullOrWhiteSpace(request.TypeId)
			&& string.IsNullOrWhiteSpace(request.CategoryId)
			&& string.IsNullOrWhiteSpace(request.SubcategoryId))
		{
			throw new ArgumentException("A team routing assignment must be default or target a type, category, or subcategory.");
		}
	}

	private static string? NormalizeOptional(string? value) =>
		string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
