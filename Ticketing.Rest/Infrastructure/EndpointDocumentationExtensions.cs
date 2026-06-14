using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Ticketing.Rest.Infrastructure;

internal static class EndpointDocumentationExtensions
{
	public static RouteHandlerBuilder WithOkDocs<TResponse>(
		this RouteHandlerBuilder builder,
		string summary,
		string description,
		bool notFound = false,
		bool conflict = false) =>
		builder
			.WithSummary(summary)
			.WithDescription(description)
			.Produces<TResponse>(StatusCodes.Status200OK)
			.WithProblemResponses(notFound, conflict);

	public static RouteHandlerBuilder WithCreatedDocs<TResponse>(
		this RouteHandlerBuilder builder,
		string summary,
		string description) =>
		builder
			.WithSummary(summary)
			.WithDescription(description)
			.Produces<TResponse>(StatusCodes.Status201Created)
			.WithProblemResponses(notFound: true, conflict: true);

	public static RouteHandlerBuilder WithNoContentDocs(
		this RouteHandlerBuilder builder,
		string summary,
		string description) =>
		builder
			.WithSummary(summary)
			.WithDescription(description)
			.Produces(StatusCodes.Status204NoContent)
			.WithProblemResponses(notFound: true, conflict: true);

	public static RouteHandlerBuilder WithFileDocs(
		this RouteHandlerBuilder builder,
		string summary,
		string description) =>
		builder
			.WithSummary(summary)
			.WithDescription(description)
			.Produces(StatusCodes.Status200OK, contentType: "application/octet-stream")
			.WithProblemResponses(notFound: true, conflict: true);

	public static RouteHandlerBuilder WithUploadDocs<TResponse>(
		this RouteHandlerBuilder builder,
		string summary,
		string description) =>
		builder
			.WithSummary(summary)
			.WithDescription(description)
			.Produces<TResponse>(StatusCodes.Status201Created)
			.ProducesProblem(StatusCodes.Status413PayloadTooLarge)
			.WithProblemResponses(notFound: true, conflict: true);

	private static RouteHandlerBuilder WithProblemResponses(
		this RouteHandlerBuilder builder,
		bool notFound,
		bool conflict)
	{
		builder
			.ProducesProblem(StatusCodes.Status400BadRequest)
			.ProducesProblem(StatusCodes.Status401Unauthorized)
			.ProducesProblem(StatusCodes.Status403Forbidden)
			.ProducesProblem(StatusCodes.Status500InternalServerError);

		if (notFound)
		{
			builder.ProducesProblem(StatusCodes.Status404NotFound);
		}

		if (conflict)
		{
			builder.ProducesProblem(StatusCodes.Status409Conflict);
		}

		return builder;
	}
}
