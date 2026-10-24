using EthioServe.API.Authorization;
using EthioServe.Application.Common;
using EthioServe.Application.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EthioServe.API.Controllers;

/// <summary>A customer's own service requests. There is no customer id in any route: it comes from the token.</summary>
[ApiController]
[Authorize(Policy = Policies.CustomerOnly)]
[Route("api/requests")]
public sealed class RequestsController(ICustomerRequestService requests) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<RequestDto>> Create(CreateJobRequest request, CancellationToken ct)
    {
        var created = await requests.CreateAsync(request, ct);

        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<RequestListItemDto>>> List([FromQuery] RequestListQuery query, CancellationToken ct) =>
        Ok(await requests.ListAsync(query, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<RequestDto>> Get(Guid id, CancellationToken ct) =>
        Ok(await requests.GetAsync(id, ct));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<RequestDto>> Update(Guid id, UpdateJobRequest request, CancellationToken ct) =>
        Ok(await requests.UpdateAsync(id, request, ct));

    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<RequestDto>> Cancel(Guid id, CancelJobRequest request, CancellationToken ct) =>
        Ok(await requests.CancelAsync(id, request, ct));
}