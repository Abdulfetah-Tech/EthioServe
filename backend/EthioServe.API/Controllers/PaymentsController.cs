using EthioServe.API.Authorization;
using EthioServe.Application.Common;
using EthioServe.Application.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EthioServe.API.Controllers;

/// <summary>Payments for accepted quotes. Only the customer can create payments; both parties can view them.</summary>
[ApiController]
[Authorize(Policy = Policies.CustomerOnly)]
[Route("api/requests/{serviceRequestId:guid}/payments")]
public sealed class PaymentsController(IPaymentService payments) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<PaymentDto>> Create(
        Guid serviceRequestId, CreatePaymentRequest request, CancellationToken ct) =>
        Ok(await payments.CreateAsync(serviceRequestId, request, ct));

    [HttpGet]
    public async Task<ActionResult<PagedResult<PaymentDto>>> List(
        Guid serviceRequestId, [FromQuery] PaymentQuery query, CancellationToken ct) =>
        Ok(await payments.ListAsync(serviceRequestId, query, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PaymentDto>> Get(Guid serviceRequestId, Guid id, CancellationToken ct) =>
        Ok(await payments.GetAsync(id, ct));
}
