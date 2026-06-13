using System.Runtime.CompilerServices;
using Azure.Data.Tables;
using Ticketing.Data.AzureStorage.Entities;
using Ticketing.Data.AzureStorage.Internal;
using Ticketing.Data.Models;
using Ticketing.Data.Stores;

namespace Ticketing.Data.AzureStorage;

internal sealed class AzureUserProfileStore : IUserProfileStore
{
	private readonly AzureStorageClients _clients;

	public AzureUserProfileStore(AzureStorageClients clients)
	{
		_clients = clients;
	}

	public async Task<TicketUserProfile> UpsertAsync(UpsertUserProfileRequest request, CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(request.UserOid);
		ArgumentException.ThrowIfNullOrWhiteSpace(request.DisplayName);

		var entity = new UserProfileEntity
		{
			PartitionKey = StorageKeys.UserProfilePartition(),
			RowKey = StorageKeys.UserProfileRow(request.UserOid),
			UserOid = request.UserOid.Trim(),
			DisplayName = request.DisplayName.Trim(),
			Email = NormalizeOptional(request.Email),
			Department = NormalizeOptional(request.Department),
			JobTitle = NormalizeOptional(request.JobTitle),
			IsActive = request.IsActive,
			LastSeenUtc = DateTimeOffset.UtcNow
		};

		await _clients.UserProfiles.UpsertEntityAsync(entity, Azure.Data.Tables.TableUpdateMode.Replace, cancellationToken);
		return entity.ToRecord();
	}

	public async Task<TicketUserProfile?> GetAsync(string userOid, CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(userOid);

		var response = await _clients.UserProfiles.GetEntityIfExistsAsync<UserProfileEntity>(
			StorageKeys.UserProfilePartition(),
			StorageKeys.UserProfileRow(userOid),
			cancellationToken: cancellationToken);

		return response.HasValue ? response.Value!.ToRecord() : null;
	}

	public async IAsyncEnumerable<TicketUserProfile> SearchAsync(
		string? query,
		bool includeInactive = false,
		int? pageSize = null,
		[EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		var normalizedQuery = NormalizeOptional(query);
		var filter = TableClient.CreateQueryFilter($"PartitionKey eq {StorageKeys.UserProfilePartition()}");
		var normalizedPageSize = AzureTablePageLimits.Normalize(pageSize);
		var returned = 0;

		await foreach (var entity in _clients.UserProfiles
			.QueryAsync<UserProfileEntity>(filter, maxPerPage: normalizedPageSize, cancellationToken: cancellationToken)
			.ConfigureAwait(false))
		{
			if (!includeInactive && !entity.IsActive)
			{
				continue;
			}

			if (!Matches(entity, normalizedQuery))
			{
				continue;
			}

			yield return entity.ToRecord();
			returned++;

			if (AzureTablePageLimits.IsFull(normalizedPageSize, returned))
			{
				yield break;
			}
		}
	}

	private static string? NormalizeOptional(string? value) =>
		string.IsNullOrWhiteSpace(value) ? null : value.Trim();

	private static bool Matches(UserProfileEntity entity, string? query)
	{
		if (string.IsNullOrWhiteSpace(query))
		{
			return true;
		}

		return Contains(entity.UserOid, query)
			|| Contains(entity.DisplayName, query)
			|| Contains(entity.Email, query)
			|| Contains(entity.Department, query)
			|| Contains(entity.JobTitle, query);
	}

	private static bool Contains(string? value, string query) =>
		value?.Contains(query, StringComparison.OrdinalIgnoreCase) == true;
}
