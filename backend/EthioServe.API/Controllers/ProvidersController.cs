using EthioServe.Application.Common;
using EthioServe.Application.Providers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EthioServe.API.Controllers;

/// <summary>The public marketplace: verified providers only, and no private contact data.</summary>
[ApiController]
[AllowAnonymous]
public sealed class ProvidersController(IProviderSearchQueries providers) : ControllerBase
{
    [HttpGet("api/providers")]
    public async Task<ActionResult<PagedResult<ProviderSummaryDto>>> Search(
        [FromQuery] ProviderSearchQuery query, CancellationToken ct) =>
        Ok(await providers.SearchAsync(query, ct));

    [HttpGet("api/providers/{id:guid}")]
    public async Task<ActionResult<PublicProviderDto>> Get(Guid id, CancellationToken ct) =>
        Ok(await providers.GetProviderAsync(id, ct));
}