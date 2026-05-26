using Microsoft.AspNetCore.Http;
using Ticketing.Domain.Models;

namespace Ticketing.Rest.Infrastructure;

internal static class DomainHttpResultMapper
{
	public static IResult ToResult<T>(DomainResult<T> result)
	{
		if (result.IsFailure)
		{
			return ToProblem(result.Error!);
		}

		return result.Value is null
			? Results.StatusCode(StatusCodes.Status500InternalServerError)
			: Results.Ok(result.Value);
	}

	public static IResult ToCreated<T>(DomainResult<T> result, Func<T, string> locationFactory)
	{
		if (result.IsFailure)
		{
			return ToProblem(result.Error!);
		}

		return result.Value is null
			? Results.StatusCode(StatusCodes.Status500InternalServerError)
			: Results.Created(locationFactory(result.Value), result.Value);
	}

	public static IResult ToNoContent(DomainResult result)
	{
		if (result.IsFailure)
		{
			return ToProblem(result.Error!);
		}

		return Results.NoContent();
	}

	public static IResult ToFile(DomainResult<Stream> result, string contentType, string fileDownloadName)
	{
		if (result.IsFailure)
		{
			return ToProblem(result.Error!);
		}

		return result.Value is null
			? Results.StatusCode(StatusCodes.Status500InternalServerError)
			: Results.File(result.Value, contentType, fileDownloadName);
	}

	public static IResult ToProblem(DomainError error)
	{
		var statusCode = error.Type switch
		{
			DomainErrorType.AuthenticationRequired or DomainErrorType.InvalidPrincipal => StatusCodes.Status401Unauthorized,
			DomainErrorType.Forbidden => StatusCodes.Status403Forbidden,
			DomainErrorType.NotFound => StatusCodes.Status404NotFound,
			DomainErrorType.Validation => StatusCodes.Status400BadRequest,
			DomainErrorType.Conflict => StatusCodes.Status409Conflict,
			_ => StatusCodes.Status500InternalServerError
		};

		var title = error.Type switch
		{
			DomainErrorType.AuthenticationRequired => "Authentication required",
			DomainErrorType.InvalidPrincipal => "Invalid principal",
			DomainErrorType.Forbidden => "Forbidden",
			DomainErrorType.NotFound => "Not found",
			DomainErrorType.Validation => "Validation failed",
			DomainErrorType.Conflict => "Conflict",
			_ => "Unexpected error"
		};

		return Results.Problem(
			statusCode: statusCode,
			title: title,
			detail: error.Message,
			extensions: new Dictionary<string, object?>
			{
				["code"] = error.Code,
				["resourceName"] = error.ResourceName,
				["resourceId"] = error.ResourceId
			});
	}
}
