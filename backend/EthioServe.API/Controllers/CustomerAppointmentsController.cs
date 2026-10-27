using EthioServe.API.Authorization;
using EthioServe.Application.Appointments;
using EthioServe.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EthioServe.API.Controllers;

/// <summary>A customer's own appointments. Another customer's appointment is a 404.</summary>
[ApiController]
[Authorize(Policy = Policies.CustomerOnly)]
[Route("api/appointments")]
public sealed class CustomerAppointmentsController(ICustomerAppointmentService appointments) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<CustomerAppointmentDto>>> List(
        [FromQuery] AppointmentQuery query, CancellationToken ct) =>
        Ok(await appointments.ListAsync(query, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CustomerAppointmentDto>> Get(Guid id, CancellationToken ct) =>
        Ok(await appointments.GetAsync(id, ct));

    [HttpPost("{id:guid}/confirm")]
    public async Task<ActionResult<CustomerAppointmentDto>> Confirm(Guid id, CancellationToken ct) =>
        Ok(await appointments.ConfirmAsync(id, ct));

    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<CustomerAppointmentDto>> Cancel(Guid id, CancellationToken ct) =>
        Ok(await appointments.CancelAsync(id, ct));
}