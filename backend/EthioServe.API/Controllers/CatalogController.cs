using EthioServe.Application.Catalog;
using EthioServe.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EthioServe.API.Controllers;

/// <summary>Public catalog of service categories and services (active items only).</summary>
[ApiController]
[AllowAnonymous]
public sealed class CatalogController(ICatalogQueries catalog) : ControllerBase
{
    [HttpGet("api/categories")]
    public async Task<ActionResult<IReadOnlyList<CategoryDto>>> Categories(CancellationToken ct) =>
        Ok(await catalog.GetCategoriesAsync(ct));

    [HttpGet("api/categories/{id:guid}")]
    public async Task<ActionResult<CategoryDto>> Category(Guid id, CancellationToken ct) =>
        Ok(await catalog.GetCategoryAsync(id, ct));

    [HttpGet("api/services")]
    public async Task<ActionResult<PagedResult<ServiceDto>>> Services([FromQuery] ServiceQuery query, CancellationToken ct) =>
        Ok(await catalog.SearchServicesAsync(query, ct));

    [HttpGet("api/services/{id:guid}")]
    public async Task<ActionResult<ServiceDto>> Service(Guid id, CancellationToken ct) =>
        Ok(await catalog.GetServiceAsync(id, ct));
}