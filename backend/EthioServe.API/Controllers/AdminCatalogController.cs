using EthioServe.API.Authorization;
using EthioServe.Application.Catalog;
using EthioServe.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EthioServe.API.Controllers;

/// <summary>Catalog management. Admin only: anonymous callers get 401, other roles get 403.</summary>
[ApiController]
[Authorize(Policy = Policies.AdminOnly)]
[Route("api/admin")]
public sealed class AdminCatalogController(IAdminCatalogService catalog) : ControllerBase
{
    [HttpGet("categories")]
    public async Task<ActionResult<IReadOnlyList<AdminCategoryDto>>> Categories(CancellationToken ct) =>
        Ok(await catalog.GetCategoriesAsync(ct));

    [HttpGet("categories/{id:guid}")]
    public async Task<ActionResult<AdminCategoryDto>> GetCategory(Guid id, CancellationToken ct) =>
        Ok(await catalog.GetCategoryAsync(id, ct));

    [HttpPost("categories")]
    public async Task<ActionResult<AdminCategoryDto>> CreateCategory(CategoryRequest request, CancellationToken ct)
    {
        var created = await catalog.CreateCategoryAsync(request, ct);

        return CreatedAtAction(nameof(GetCategory), new { id = created.Id }, created);
    }

    [HttpPut("categories/{id:guid}")]
    public async Task<ActionResult<AdminCategoryDto>> UpdateCategory(Guid id, CategoryRequest request, CancellationToken ct) =>
        Ok(await catalog.UpdateCategoryAsync(id, request, ct));

    [HttpPost("categories/{id:guid}/deactivate")]
    public async Task<ActionResult<AdminCategoryDto>> DeactivateCategory(Guid id, CancellationToken ct) =>
        Ok(await catalog.SetCategoryActiveAsync(id, false, ct));

    [HttpPost("categories/{id:guid}/activate")]
    public async Task<ActionResult<AdminCategoryDto>> ActivateCategory(Guid id, CancellationToken ct) =>
        Ok(await catalog.SetCategoryActiveAsync(id, true, ct));

    [HttpGet("services")]
    public async Task<ActionResult<PagedResult<AdminServiceDto>>> Services([FromQuery] AdminServiceQuery query, CancellationToken ct) =>
        Ok(await catalog.SearchServicesAsync(query, ct));

    [HttpGet("services/{id:guid}")]
    public async Task<ActionResult<AdminServiceDto>> GetService(Guid id, CancellationToken ct) =>
        Ok(await catalog.GetServiceAsync(id, ct));

    [HttpPost("services")]
    public async Task<ActionResult<AdminServiceDto>> CreateService(CreateServiceRequest request, CancellationToken ct)
    {
        var created = await catalog.CreateServiceAsync(request, ct);

        return CreatedAtAction(nameof(GetService), new { id = created.Id }, created);
    }

    [HttpPut("services/{id:guid}")]
    public async Task<ActionResult<AdminServiceDto>> UpdateService(Guid id, UpdateServiceRequest request, CancellationToken ct) =>
        Ok(await catalog.UpdateServiceAsync(id, request, ct));

    [HttpPost("services/{id:guid}/deactivate")]
    public async Task<ActionResult<AdminServiceDto>> DeactivateService(Guid id, CancellationToken ct) =>
        Ok(await catalog.SetServiceActiveAsync(id, false, ct));

    [HttpPost("services/{id:guid}/activate")]
    public async Task<ActionResult<AdminServiceDto>> ActivateService(Guid id, CancellationToken ct) =>
        Ok(await catalog.SetServiceActiveAsync(id, true, ct));
}