using EthioServe.API.Authorization;
using EthioServe.Application.Common;
using EthioServe.Application.Quotes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EthioServe.API.Controllers;

/// <summary>A provider's own quotes. There is no provider id in any route: it comes from the token.</summary>
[ApiController]
[Authorize(Policy = Policies.ProviderOnly)]
[Route("api/providers/me")]
public sealed class ProviderQuotesController(IProviderQuoteService quotes) : ControllerBase
{
    [HttpPost("requests/{requestId:guid}/quotes")]
    public async Task<ActionResult<ProviderQuoteDto>> Submit(Guid requestId, SubmitQuoteRequest request, CancellationToken ct)
    {
        var created = await quotes.SubmitAsync(requestId, request, ct);

        return Created($"/api/providers/me/quotes/{created.Id}", created);
    }

    [HttpGet("quotes")]
    public async Task<ActionResult<PagedResult<ProviderQuoteDto>>> List([FromQuery] ProviderQuoteQuery query, CancellationToken ct) =>
        Ok(await quotes.ListAsync(query, ct));

    [HttpGet("quotes/{id:guid}")]
    public async Task<ActionResult<ProviderQuoteDto>> Get(Guid id, CancellationToken ct) =>
        Ok(await quotes.GetAsync(id, ct));

    [HttpPost("quotes/{id:guid}/withdraw")]
    public async Task<ActionResult<ProviderQuoteDto>> Withdraw(Guid id, CancellationToken ct) =>
        Ok(await quotes.WithdrawAsync(id, ct));
}