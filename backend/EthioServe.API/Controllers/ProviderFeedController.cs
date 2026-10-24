using EthioServe.API.Authorization;
using EthioServe.Application.Common;
using EthioServe.Application.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EthioServe.API.Controllers;

/// <summary>The open customer requests a verified provider can respond to. No provider id in any route.</summary>
[ApiController]
[Authorize(Policy = Policies.ProviderOnly)]
[Route("api/providers/me/requests")]
public sealed class ProviderFeedController(IProviderFeedService feed) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<FeedItemDto>>> List([FromQuery] FeedQuery query, CancellationToken ct) =>
        Ok(await feed.ListAsync(query, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<FeedDetailDto>> Get(Guid id, CancellationToken ct) =>
        Ok(await feed.GetAsync(id, ct));
}