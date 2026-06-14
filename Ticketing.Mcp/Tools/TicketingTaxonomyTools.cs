using System.ComponentModel;
using ModelContextProtocol.Server;
using Ticketing.Auth;
using Ticketing.Data.Models;
using Ticketing.Domain.Models;
using Ticketing.Domain.Services;
using Ticketing.Mcp.Contracts;
using Ticketing.Mcp.Infrastructure;

namespace Ticketing.Mcp.Tools;

[McpServerToolType]
public sealed class TicketingTaxonomyTools
{
	private readonly TicketingMcpAuthorizationService _authorization;
	private readonly ITaxonomyManagementService _taxonomy;

	public TicketingTaxonomyTools(
		TicketingMcpAuthorizationService authorization,
		ITaxonomyManagementService taxonomy)
	{
		_authorization = authorization;
		_taxonomy = taxonomy;
	}

	[McpServerTool(Name = "ticketing_save_ticket_type")]
	[Description("Creates or updates a top-level ticket type.")]
	public async Task<TicketingMcpToolResult<TicketTypeRecord>> SaveTicketTypeAsync(
		[Description("Type name.")] string name,
		[Description("Optional type id to update. Leave null to create a new type.")] string? typeId = null,
		[Description("Optional type description.")] string? description = null,
		[Description("Sort order for display.")] int sortOrder = 0,
		[Description("Whether the type is active.")] bool isActive = true,
		CancellationToken cancellationToken = default)
	{
		return await _authorization.RunAsync(
			TicketingAuthPolicies.ManageTaxonomy,
			ct => _taxonomy.SaveTypeAsync(
				new SaveTicketTypeCommand
				{
					TypeId = typeId,
					Name = name,
					Description = description,
					SortOrder = sortOrder,
					IsActive = isActive
				},
				ct),
			cancellationToken);
	}

	[McpServerTool(Name = "ticketing_list_ticket_types", ReadOnly = true)]
	[Description("Lists top-level ticket taxonomy types.")]
	public async Task<TicketingMcpToolResult<PagedResult<TicketTypeRecord>>> ListTicketTypesAsync(
		[Description("Include inactive retired values when true.")] bool includeInactive = false,
		[Description("Requested page size.")] int? pageSize = null,
		[Description("Continuation token from a previous response's nextPageToken.")] string? pageToken = null,
		CancellationToken cancellationToken = default)
	{
		return await _authorization.RunAsync(
			TicketingAuthPolicies.Read,
			ct => _taxonomy.GetTypesAsync(includeInactive, pageSize, pageToken, ct),
			cancellationToken);
	}

	[McpServerTool(Name = "ticketing_save_ticket_category")]
	[Description("Creates or updates a ticket category under a type.")]
	public async Task<TicketingMcpToolResult<TicketCategoryRecord>> SaveTicketCategoryAsync(
		[Description("Parent type id.")] string typeId,
		[Description("Category name.")] string name,
		[Description("Optional category id to update. Leave null to create a new category.")] string? categoryId = null,
		[Description("Optional category description.")] string? description = null,
		[Description("Sort order for display.")] int sortOrder = 0,
		[Description("Whether the category is active.")] bool isActive = true,
		CancellationToken cancellationToken = default)
	{
		return await _authorization.RunAsync(
			TicketingAuthPolicies.ManageTaxonomy,
			ct => _taxonomy.SaveCategoryAsync(
				new SaveTicketCategoryCommand
				{
					CategoryId = categoryId,
					TypeId = typeId,
					Name = name,
					Description = description,
					SortOrder = sortOrder,
					IsActive = isActive
				},
				ct),
			cancellationToken);
	}

	[McpServerTool(Name = "ticketing_list_ticket_categories", ReadOnly = true)]
	[Description("Lists categories under a ticket type.")]
	public async Task<TicketingMcpToolResult<PagedResult<TicketCategoryRecord>>> ListTicketCategoriesAsync(
		[Description("Parent type id.")] string typeId,
		[Description("Include inactive retired values when true.")] bool includeInactive = false,
		[Description("Requested page size.")] int? pageSize = null,
		[Description("Continuation token from a previous response's nextPageToken.")] string? pageToken = null,
		CancellationToken cancellationToken = default)
	{
		return await _authorization.RunAsync(
			TicketingAuthPolicies.Read,
			ct => _taxonomy.GetCategoriesAsync(typeId, includeInactive, pageSize, pageToken, ct),
			cancellationToken);
	}

	[McpServerTool(Name = "ticketing_save_ticket_subcategory")]
	[Description("Creates or updates a ticket subcategory under a type/category pair.")]
	public async Task<TicketingMcpToolResult<TicketSubcategoryRecord>> SaveTicketSubcategoryAsync(
		[Description("Parent type id.")] string typeId,
		[Description("Parent category id.")] string categoryId,
		[Description("Subcategory name.")] string name,
		[Description("Optional subcategory id to update. Leave null to create a new subcategory.")] string? subcategoryId = null,
		[Description("Optional subcategory description.")] string? description = null,
		[Description("Sort order for display.")] int sortOrder = 0,
		[Description("Whether the subcategory is active.")] bool isActive = true,
		CancellationToken cancellationToken = default)
	{
		return await _authorization.RunAsync(
			TicketingAuthPolicies.ManageTaxonomy,
			ct => _taxonomy.SaveSubcategoryAsync(
				new SaveTicketSubcategoryCommand
				{
					SubcategoryId = subcategoryId,
					TypeId = typeId,
					CategoryId = categoryId,
					Name = name,
					Description = description,
					SortOrder = sortOrder,
					IsActive = isActive
				},
				ct),
			cancellationToken);
	}

	[McpServerTool(Name = "ticketing_list_ticket_subcategories", ReadOnly = true)]
	[Description("Lists subcategories under a ticket category.")]
	public async Task<TicketingMcpToolResult<PagedResult<TicketSubcategoryRecord>>> ListTicketSubcategoriesAsync(
		[Description("Parent category id.")] string categoryId,
		[Description("Include inactive retired values when true.")] bool includeInactive = false,
		[Description("Requested page size.")] int? pageSize = null,
		[Description("Continuation token from a previous response's nextPageToken.")] string? pageToken = null,
		CancellationToken cancellationToken = default)
	{
		return await _authorization.RunAsync(
			TicketingAuthPolicies.Read,
			ct => _taxonomy.GetSubcategoriesAsync(categoryId, includeInactive, pageSize, pageToken, ct),
			cancellationToken);
	}
}
