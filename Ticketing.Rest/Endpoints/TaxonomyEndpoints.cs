using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Ticketing.Auth;
using Ticketing.Data.Models;
using Ticketing.Domain.Models;
using Ticketing.Domain.Services;
using Ticketing.Rest.Contracts;
using Ticketing.Rest.Infrastructure;

namespace Ticketing.Rest.Endpoints;

internal static class TaxonomyEndpoints
{
	public static RouteGroupBuilder MapTaxonomyEndpoints(this RouteGroupBuilder api)
	{
		var taxonomy = api.MapGroup("/taxonomy")
			.WithTags("Taxonomy")
			.RequireAuthorization(TicketingAuthPolicies.Read);

		taxonomy.MapGet("/types", async (
				bool includeInactive,
				int? pageSize,
				string? pageToken,
				ITaxonomyManagementService taxonomyManagement,
				CancellationToken cancellationToken) =>
			{
				var result = await taxonomyManagement.GetTypesAsync(includeInactive, pageSize, pageToken, cancellationToken);
				return DomainHttpResultMapper.ToResult(result);
			})
			.WithName("GetTicketTypes")
			.WithOkDocs<PagedResult<TicketTypeRecord>>(
				"List ticket types",
				"Returns top-level ticket taxonomy types. Set includeInactive to true for administrative screens that need retired values; use pageSize and pageToken for paging.");

		taxonomy.MapPost("/types", async (
				SaveTicketTypeHttpRequest request,
				ITaxonomyManagementService taxonomyManagement,
				CancellationToken cancellationToken) =>
			{
				var result = await taxonomyManagement.SaveTypeAsync(
					new SaveTicketTypeCommand
					{
						TypeId = request.TypeId,
						Name = request.Name,
						Description = request.Description,
						SortOrder = request.SortOrder,
						IsActive = request.IsActive
					},
					cancellationToken);

				return DomainHttpResultMapper.ToCreated(result, type => $"/api/taxonomy/types/{type.TypeId}");
			})
			.RequireAuthorization(TicketingAuthPolicies.ManageTaxonomy)
			.WithName("SaveTicketType")
			.WithCreatedDocs<TicketTypeRecord>(
				"Create or upsert a ticket type",
				"Creates a ticket type or updates the requested TypeId when supplied in the request body. Types are the top-level classification used by routing and reporting.");

		taxonomy.MapPut("/types/{typeId}", async (
				string typeId,
				SaveTicketTypeHttpRequest request,
				ITaxonomyManagementService taxonomyManagement,
				CancellationToken cancellationToken) =>
			{
				var result = await taxonomyManagement.SaveTypeAsync(
					new SaveTicketTypeCommand
					{
						TypeId = typeId,
						Name = request.Name,
						Description = request.Description,
						SortOrder = request.SortOrder,
						IsActive = request.IsActive
					},
					cancellationToken);

				return DomainHttpResultMapper.ToResult(result);
			})
			.RequireAuthorization(TicketingAuthPolicies.ManageTaxonomy)
			.WithName("UpdateTicketType")
			.WithOkDocs<TicketTypeRecord>(
				"Update a ticket type",
				"Updates the name, description, sort order, and active state for an existing type id.",
				conflict: true);

		taxonomy.MapGet("/types/{typeId}/categories", async (
				string typeId,
				bool includeInactive,
				int? pageSize,
				string? pageToken,
				ITaxonomyManagementService taxonomyManagement,
				CancellationToken cancellationToken) =>
			{
				var result = await taxonomyManagement.GetCategoriesAsync(typeId, includeInactive, pageSize, pageToken, cancellationToken);
				return DomainHttpResultMapper.ToResult(result);
			})
			.WithName("GetTicketCategories")
			.WithOkDocs<PagedResult<TicketCategoryRecord>>(
				"List ticket categories",
				"Returns categories under a ticket type. Set includeInactive to true for setup/admin views; use pageSize and pageToken for paging.");

		taxonomy.MapPost("/types/{typeId}/categories", async (
				string typeId,
				SaveTicketCategoryHttpRequest request,
				ITaxonomyManagementService taxonomyManagement,
				CancellationToken cancellationToken) =>
			{
				var result = await taxonomyManagement.SaveCategoryAsync(
					new SaveTicketCategoryCommand
					{
						CategoryId = request.CategoryId,
						TypeId = typeId,
						Name = request.Name,
						Description = request.Description,
						SortOrder = request.SortOrder,
						IsActive = request.IsActive
					},
					cancellationToken);

				return DomainHttpResultMapper.ToCreated(
					result,
					category => $"/api/taxonomy/types/{category.TypeId}/categories/{category.CategoryId}");
			})
			.RequireAuthorization(TicketingAuthPolicies.ManageTaxonomy)
			.WithName("SaveTicketCategory")
			.WithCreatedDocs<TicketCategoryRecord>(
				"Create or upsert a ticket category",
				"Creates a category under the route typeId or updates the requested CategoryId when supplied in the request body. Categories are used by routing, queues, and search.");

		taxonomy.MapPut("/types/{typeId}/categories/{categoryId}", async (
				string typeId,
				string categoryId,
				SaveTicketCategoryHttpRequest request,
				ITaxonomyManagementService taxonomyManagement,
				CancellationToken cancellationToken) =>
			{
				var result = await taxonomyManagement.SaveCategoryAsync(
					new SaveTicketCategoryCommand
					{
						CategoryId = categoryId,
						TypeId = typeId,
						Name = request.Name,
						Description = request.Description,
						SortOrder = request.SortOrder,
						IsActive = request.IsActive
					},
					cancellationToken);

				return DomainHttpResultMapper.ToResult(result);
			})
			.RequireAuthorization(TicketingAuthPolicies.ManageTaxonomy)
			.WithName("UpdateTicketCategory")
			.WithOkDocs<TicketCategoryRecord>(
				"Update a ticket category",
				"Updates the name, description, sort order, and active state for a category under the route typeId.",
				conflict: true);

		taxonomy.MapGet("/types/{typeId}/categories/{categoryId}/subcategories", async (
				string categoryId,
				bool includeInactive,
				int? pageSize,
				string? pageToken,
				ITaxonomyManagementService taxonomyManagement,
				CancellationToken cancellationToken) =>
			{
				var result = await taxonomyManagement.GetSubcategoriesAsync(categoryId, includeInactive, pageSize, pageToken, cancellationToken);
				return DomainHttpResultMapper.ToResult(result);
			})
			.WithName("GetTicketSubcategories")
			.WithOkDocs<PagedResult<TicketSubcategoryRecord>>(
				"List ticket subcategories",
				"Returns subcategories under a ticket category. Subcategories are the most specific classification level used by routing.");

		taxonomy.MapPost("/types/{typeId}/categories/{categoryId}/subcategories", async (
				string typeId,
				string categoryId,
				SaveTicketSubcategoryHttpRequest request,
				ITaxonomyManagementService taxonomyManagement,
				CancellationToken cancellationToken) =>
			{
				var result = await taxonomyManagement.SaveSubcategoryAsync(
					new SaveTicketSubcategoryCommand
					{
						SubcategoryId = request.SubcategoryId,
						TypeId = typeId,
						CategoryId = categoryId,
						Name = request.Name,
						Description = request.Description,
						SortOrder = request.SortOrder,
						IsActive = request.IsActive
					},
					cancellationToken);

				return DomainHttpResultMapper.ToCreated(
					result,
					subcategory => $"/api/taxonomy/types/{subcategory.TypeId}/categories/{subcategory.CategoryId}/subcategories/{subcategory.SubcategoryId}");
			})
			.RequireAuthorization(TicketingAuthPolicies.ManageTaxonomy)
			.WithName("SaveTicketSubcategory")
			.WithCreatedDocs<TicketSubcategoryRecord>(
				"Create or upsert a ticket subcategory",
				"Creates a subcategory under the route type/category or updates the requested SubcategoryId when supplied in the request body. Subcategories can be used for the most specific team routing rules.");

		taxonomy.MapPut("/types/{typeId}/categories/{categoryId}/subcategories/{subcategoryId}", async (
				string typeId,
				string categoryId,
				string subcategoryId,
				SaveTicketSubcategoryHttpRequest request,
				ITaxonomyManagementService taxonomyManagement,
				CancellationToken cancellationToken) =>
			{
				var result = await taxonomyManagement.SaveSubcategoryAsync(
					new SaveTicketSubcategoryCommand
					{
						SubcategoryId = subcategoryId,
						TypeId = typeId,
						CategoryId = categoryId,
						Name = request.Name,
						Description = request.Description,
						SortOrder = request.SortOrder,
						IsActive = request.IsActive
					},
					cancellationToken);

				return DomainHttpResultMapper.ToResult(result);
			})
			.RequireAuthorization(TicketingAuthPolicies.ManageTaxonomy)
			.WithName("UpdateTicketSubcategory")
			.WithOkDocs<TicketSubcategoryRecord>(
				"Update a ticket subcategory",
				"Updates the name, description, sort order, and active state for a subcategory under the route type/category.",
				conflict: true);

		return taxonomy;
	}
}
