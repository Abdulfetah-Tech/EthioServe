using EthioServe.API.Authorization;
using EthioServe.Application.Common;
using EthioServe.Application.Providers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EthioServe.API.Controllers;

/// <summary>Provider verification. Admin only: anonymous callers get 401, other roles get 403.</summary>
[ApiController]
[Authorize(Policy = Policies.AdminOnly)]
[Route("api/admin/providers")]
public sealed class AdminProvidersController(IAdminProviderService providers) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<AdminProviderListItemDto>>> Search(
        [FromQuery] AdminProviderQuery query, CancellationToken ct) =>
        Ok(await providers.SearchAsync(query, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AdminProviderDetailDto>> Get(Guid id, CancellationToken ct) =>
        Ok(await providers.GetAsync(id, ct));

    [HttpPost("{id:guid}/approve")]
    public async Task<ActionResult<AdminProviderDetailDto>> Approve(Guid id, CancellationToken ct) =>
        Ok(await providers.ApproveAsync(id, ct));

    [HttpPost("{id:guid}/reject")]
    public async Task<ActionResult<AdminProviderDetailDto>> Reject(Guid id, ReviewDecisionRequest request, CancellationToken ct) =>
        Ok(await providers.RejectAsync(id, request, ct));

    [HttpPost("{id:guid}/suspend")]
    public async Task<ActionResult<AdminProviderDetailDto>> Suspend(Guid id, ReviewDecisionRequest request, CancellationToken ct) =>
        Ok(await providers.SuspendAsync(id, request, ct));

    [HttpPost("{id:guid}/reinstate")]
    public async Task<ActionResult<AdminProviderDetailDto>> Reinstate(Guid id, CancellationToken ct) =>
        Ok(await providers.ReinstateAsync(id, ct));
}