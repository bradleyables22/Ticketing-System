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

	private static string? NormalizeOptional(string? value) =>
		string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
