using EthioServe.API.Authorization;
using EthioServe.Application.Quotes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EthioServe.API.Controllers;

/// <summary>The quotes on a customer's own request. Another customer's request is a 404.</summary>
[ApiController]
[Authorize(Policy = Policies.CustomerOnly)]
[Route("api/requests/{requestId:guid}/quotes")]
public sealed class CustomerQuotesController(ICustomerQuoteService quotes) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<QuoteDto>>> List(Guid requestId, CancellationToken ct) =>
        Ok(await quotes.ListAsync(requestId, ct));

    [HttpPost("{quoteId:guid}/accept")]
    public async Task<ActionResult<QuoteDto>> Accept(Guid requestId, Guid quoteId, CancellationToken ct) =>
        Ok(await quotes.AcceptAsync(requestId, quoteId, ct));

    [HttpPost("{quoteId:guid}/decline")]
    public async Task<ActionResult<QuoteDto>> Decline(Guid requestId, Guid quoteId, CancellationToken ct) =>
        Ok(await quotes.DeclineAsync(requestId, quoteId, ct));
}