using EthioServe.API.Authorization;
using EthioServe.Application.Appointments;
using EthioServe.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EthioServe.API.Controllers;

/// <summary>A provider's own appointments. There is no provider id in any route: it comes from the token.</summary>
[ApiController]
[Authorize(Policy = Policies.ProviderOnly)]
[Route("api/providers/me")]
public sealed class ProviderAppointmentsController(IProviderAppointmentService appointments) : ControllerBase
{
    [HttpPost("requests/{requestId:guid}/appointment")]
    public async Task<ActionResult<ProviderAppointmentDto>> Schedule(
        Guid requestId, ScheduleAppointmentRequest request, CancellationToken ct)
    {
        var created = await appointments.ScheduleAsync(requestId, request, ct);

        return Created($"/api/providers/me/appointments/{created.Id}", created);
    }

    [HttpGet("appointments")]
    public async Task<ActionResult<PagedResult<ProviderAppointmentDto>>> List(
        [FromQuery] AppointmentQuery query, CancellationToken ct) =>
        Ok(await appointments.ListAsync(query, ct));

    [HttpGet("appointments/{id:guid}")]
    public async Task<ActionResult<ProviderAppointmentDto>> Get(Guid id, CancellationToken ct) =>
        Ok(await appointments.GetAsync(id, ct));

    [HttpPost("appointments/{id:guid}/reschedule")]
    public async Task<ActionResult<ProviderAppointmentDto>> Reschedule(
        Guid id, RescheduleAppointmentRequest request, CancellationToken ct) =>
        Ok(await appointments.RescheduleAsync(id, request, ct));

    [HttpPost("appointments/{id:guid}/cancel")]
    public async Task<ActionResult<ProviderAppointmentDto>> Cancel(Guid id, CancellationToken ct) =>
        Ok(await appointments.CancelAsync(id, ct));

    [HttpPost("appointments/{id:guid}/start")]
    public async Task<ActionResult<ProviderAppointmentDto>> Start(Guid id, CancellationToken ct) =>
        Ok(await appointments.StartAsync(id, ct));

    [HttpPost("appointments/{id:guid}/complete")]
    public async Task<ActionResult<ProviderAppointmentDto>> Complete(Guid id, CancellationToken ct) =>
        Ok(await appointments.CompleteAsync(id, ct));
}