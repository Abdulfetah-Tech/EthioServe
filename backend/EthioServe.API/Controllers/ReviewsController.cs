using EthioServe.API.Authorization;
using EthioServe.Application.Common;
using EthioServe.Application.Reviews;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EthioServe.API.Controllers;

/// <summary>Customers review completed jobs; everyone can read visible reviews for a public provider.</summary>
[ApiController]
public sealed class ReviewsController(IReviewService reviews) : ControllerBase
{
    [HttpPost("api/requests/{requestId:guid}/reviews")]
    [Authorize(Policy = Policies.CustomerOnly)]
    public async Task<ActionResult<ProviderReviewDto>> Create(
        Guid requestId, CreateReviewRequest request, CancellationToken ct)
    {
        var review = await reviews.CreateAsync(requestId, request, ct);
        return StatusCode(StatusCodes.Status201Created, review);
    }

    [HttpGet("api/providers/{providerId:guid}/reviews")]
    [AllowAnonymous]
    public async Task<ActionResult<PagedResult<ProviderReviewDto>>> List(
        Guid providerId, [FromQuery] ProviderReviewQuery query, CancellationToken ct) =>
        Ok(await reviews.ListPublicAsync(providerId, query, ct));
}
