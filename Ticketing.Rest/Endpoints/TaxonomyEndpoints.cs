using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Ticketing.Auth;
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
				ITaxonomyManagementService taxonomyManagement,
				CancellationToken cancellationToken) =>
			{
				var result = await taxonomyManagement.GetTypesAsync(includeInactive, pageSize, cancellationToken);
				return DomainHttpResultMapper.ToResult(result);
			})
			.WithName("GetTicketTypes");

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
			.WithName("SaveTicketType");

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
			.WithName("UpdateTicketType");

		taxonomy.MapGet("/types/{typeId}/categories", async (
				string typeId,
				bool includeInactive,
				int? pageSize,
				ITaxonomyManagementService taxonomyManagement,
				CancellationToken cancellationToken) =>
			{
				var result = await taxonomyManagement.GetCategoriesAsync(typeId, includeInactive, pageSize, cancellationToken);
				return DomainHttpResultMapper.ToResult(result);
			})
			.WithName("GetTicketCategories");

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
			.WithName("SaveTicketCategory");

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
			.WithName("UpdateTicketCategory");

		taxonomy.MapGet("/types/{typeId}/categories/{categoryId}/subcategories", async (
				string categoryId,
				bool includeInactive,
				int? pageSize,
				ITaxonomyManagementService taxonomyManagement,
				CancellationToken cancellationToken) =>
			{
				var result = await taxonomyManagement.GetSubcategoriesAsync(categoryId, includeInactive, pageSize, cancellationToken);
				return DomainHttpResultMapper.ToResult(result);
			})
			.WithName("GetTicketSubcategories");

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
			.WithName("SaveTicketSubcategory");

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
			.WithName("UpdateTicketSubcategory");

		return taxonomy;
	}
}
