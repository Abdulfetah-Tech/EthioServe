using EthioServe.API.Authorization;
using EthioServe.Application.Common;
using EthioServe.Application.Favorites;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EthioServe.API.Controllers;

/// <summary>A customer's saved providers. Only the owner can list or manage their favorites.</summary>
[ApiController]
[Authorize(Policy = Policies.CustomerOnly)]
[Route("api/customers/me/favorites")]
public sealed class FavoritesController(IFavoriteService favorites) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<FavoriteProviderDto>>> List(
        [FromQuery] FavoriteQuery query, CancellationToken ct) =>
        Ok(await favorites.ListAsync(query, ct));

    [HttpPost("{providerId:guid}")]
    public async Task<IActionResult> Add(Guid providerId, CancellationToken ct)
    {
        await favorites.AddAsync(providerId, ct);

        return NoContent();
    }

    [HttpDelete("{providerId:guid}")]
    public async Task<IActionResult> Remove(Guid providerId, CancellationToken ct)
    {
        await favorites.RemoveAsync(providerId, ct);

        return NoContent();
    }
}
