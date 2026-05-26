namespace Ticketing.Domain.Exceptions;

public abstract class TicketingDomainException : Exception
{
	protected TicketingDomainException(string errorCode, string message)
		: base(message)
	{
		ErrorCode = errorCode;
	}

	public string ErrorCode { get; }
}

public sealed class TicketingAuthenticationRequiredException : TicketingDomainException
{
	public TicketingAuthenticationRequiredException()
		: base("authentication_required", "An authenticated user is required for this operation.")
	{
	}
}

public sealed class TicketingInvalidPrincipalException : TicketingDomainException
{
	public TicketingInvalidPrincipalException(string message)
		: base("invalid_principal", message)
	{
	}
}

public sealed class TicketingForbiddenException : TicketingDomainException
{
	public TicketingForbiddenException(string message)
		: base("forbidden", message)
	{
	}
}

public sealed class TicketingNotFoundException : TicketingDomainException
{
	public TicketingNotFoundException(string resourceName, string resourceId)
		: base("not_found", $"{resourceName} '{resourceId}' was not found.")
	{
		ResourceName = resourceName;
		ResourceId = resourceId;
	}

	public string ResourceName { get; }

	public string ResourceId { get; }
}

public sealed class TicketingValidationException : TicketingDomainException
{
	public TicketingValidationException(string message)
		: base("validation_error", message)
	{
	}
}
