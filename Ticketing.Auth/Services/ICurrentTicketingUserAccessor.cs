using Ticketing.Auth.Models;

namespace Ticketing.Auth.Services;

public interface ICurrentTicketingUserAccessor
{
	TicketingUser Current { get; }
}
