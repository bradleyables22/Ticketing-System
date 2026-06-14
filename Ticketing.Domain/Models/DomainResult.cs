using Ticketing.Domain.Exceptions;

namespace Ticketing.Domain.Models;

public enum DomainErrorType
{
	AuthenticationRequired,
	InvalidPrincipal,
	Forbidden,
	NotFound,
	Validation,
	PayloadTooLarge,
	Conflict,
	Unexpected
}

public sealed record DomainError
{
	public required string Code { get; init; }

	public required string Message { get; init; }

	public DomainErrorType Type { get; init; }

	public string? ResourceName { get; init; }

	public string? ResourceId { get; init; }

	public static DomainError AuthenticationRequired(string message) =>
		new()
		{
			Code = "authentication_required",
			Message = message,
			Type = DomainErrorType.AuthenticationRequired
		};

	public static DomainError InvalidPrincipal(string message) =>
		new()
		{
			Code = "invalid_principal",
			Message = message,
			Type = DomainErrorType.InvalidPrincipal
		};

	public static DomainError Forbidden(string message) =>
		new()
		{
			Code = "forbidden",
			Message = message,
			Type = DomainErrorType.Forbidden
		};

	public static DomainError NotFound(string resourceName, string resourceId, string? message = null) =>
		new()
		{
			Code = "not_found",
			Message = message ?? $"{resourceName} '{resourceId}' was not found.",
			Type = DomainErrorType.NotFound,
			ResourceName = resourceName,
			ResourceId = resourceId
		};

	public static DomainError Validation(string message) =>
		new()
		{
			Code = "validation_error",
			Message = message,
			Type = DomainErrorType.Validation
		};

	public static DomainError PayloadTooLarge(string message) =>
		new()
		{
			Code = "payload_too_large",
			Message = message,
			Type = DomainErrorType.PayloadTooLarge
		};

	public static DomainError Conflict(string message) =>
		new()
		{
			Code = "conflict",
			Message = message,
			Type = DomainErrorType.Conflict
		};

	public static DomainError Unexpected(string message) =>
		new()
		{
			Code = "unexpected_error",
			Message = message,
			Type = DomainErrorType.Unexpected
		};

	public static DomainError FromException(Exception exception) =>
		exception switch
		{
			TicketingAuthenticationRequiredException auth => AuthenticationRequired(auth.Message),
			TicketingInvalidPrincipalException invalid => InvalidPrincipal(invalid.Message),
			TicketingForbiddenException forbidden => Forbidden(forbidden.Message),
			TicketingNotFoundException notFound => NotFound(notFound.ResourceName, notFound.ResourceId, notFound.Message),
			TicketingValidationException validation => Validation(validation.Message),
			TicketingPayloadTooLargeException payloadTooLarge => PayloadTooLarge(payloadTooLarge.Message),
			ArgumentException argument => Validation(argument.Message),
			KeyNotFoundException keyNotFound => NotFound("Resource", "unknown", keyNotFound.Message),
			InvalidOperationException invalidOperation => Conflict(invalidOperation.Message),
			_ => Unexpected(exception.Message)
		};
}

public sealed record DomainResult
{
	public bool IsSuccess { get; init; }

	public DomainError? Error { get; init; }

	public bool IsFailure => !IsSuccess;

	public static DomainResult Success() => new() { IsSuccess = true };

	public static DomainResult Failure(DomainError error) => new() { Error = error };

	public static async Task<DomainResult> TryAsync(Func<Task> action)
	{
		try
		{
			await action();
			return Success();
		}
		catch (Exception ex) when (IsExpectedDomainException(ex))
		{
			return Failure(DomainError.FromException(ex));
		}
	}

	internal static bool IsExpectedDomainException(Exception exception) =>
		exception is TicketingDomainException
			or ArgumentException
			or KeyNotFoundException
			or InvalidOperationException;
}

public sealed record DomainResult<T>
{
	public bool IsSuccess { get; init; }

	public T? Value { get; init; }

	public DomainError? Error { get; init; }

	public bool IsFailure => !IsSuccess;

	public static DomainResult<T> Success(T value) => new() { IsSuccess = true, Value = value };

	public static DomainResult<T> Failure(DomainError error) => new() { Error = error };

	public static async Task<DomainResult<T>> TryAsync(Func<Task<T>> action)
	{
		try
		{
			return Success(await action());
		}
		catch (Exception ex) when (DomainResult.IsExpectedDomainException(ex))
		{
			return Failure(DomainError.FromException(ex));
		}
	}

	public static DomainResult<T> Try(Func<T> action)
	{
		try
		{
			return Success(action());
		}
		catch (Exception ex) when (DomainResult.IsExpectedDomainException(ex))
		{
			return Failure(DomainError.FromException(ex));
		}
	}
}
