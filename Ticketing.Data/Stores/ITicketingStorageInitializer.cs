namespace Ticketing.Data.Stores;

public interface ITicketingStorageInitializer
{
	Task InitializeAsync(CancellationToken cancellationToken = default);
}
