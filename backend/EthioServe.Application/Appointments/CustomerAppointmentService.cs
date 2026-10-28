using EthioServe.Application.Abstractions;
using EthioServe.Application.Common;
using EthioServe.Application.Common.Exceptions;
using EthioServe.Application.Notifications;
using EthioServe.Domain.Common;
using EthioServe.Domain.Entities;
using EthioServe.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EthioServe.Application.Appointments;

public sealed class CustomerAppointmentService(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    IAuditTrail audit,
    IClock clock,
    INotifier notifier) : ICustomerAppointmentService
{
    // The customer comes from the signed-in user's token, never from the request.
    private Guid CustomerId =>
        currentUser.CustomerId ?? throw new ForbiddenException("This account has no customer profile.");

    public async Task<PagedResult<CustomerAppointmentDto>> ListAsync(AppointmentQuery query, CancellationToken ct = default)
    {
        var customerId = CustomerId;
        var (page, pageSize) = Paging.Normalize(query.Page, query.PageSize);

        var rows = AppointmentProjections.Rows(db).Where(x => x.CustomerId == customerId);

        if (query.RequestId is { } requestId)
        {
            rows = rows.Where(x => x.RequestId == requestId);
        }

        rows = AppointmentProjections.InScope(rows, query.Scope);
        var total = await rows.CountAsync(ct);

        var pageRows = await AppointmentProjections.Ordered(rows, query.Scope)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<CustomerAppointmentDto>(pageRows.Select(ToDto).ToList(), page, pageSize, total);
    }

    // Looked up by id AND owner: another customer's appointment is simply "not found".
    public async Task<CustomerAppointmentDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var customerId = CustomerId;

        var row = await AppointmentProjections.Rows(db)
                      .Where(x => x.Id == id && x.CustomerId == customerId)
                      .FirstOrDefaultAsync(ct)
                  ?? throw new NotFoundException("Appointment not found.");

        return ToDto(row);
    }

    public async Task<CustomerAppointmentDto> ConfirmAsync(Guid id, CancellationToken ct = default)
    {
        var customerId = CustomerId;
        var appointment = await FindAsync(id, customerId, ct);
        var serviceRequest = await db.ServiceRequests.FirstAsync(r => r.Id == appointment.ServiceRequestId, ct);

        var before = Snapshot(appointment);
        appointment.Confirm(); // 422 unless it is waiting for the customer

        if (appointment.StartAt <= clock.UtcNow)
        {
            throw new DomainException("This appointment time has already passed. Ask the provider for a new time.");
        }

        audit.Record("AppointmentConfirmed", nameof(Appointment), appointment.Id, before, Snapshot(appointment));
        await notifier.ToProviderAsync(
            appointment.ProviderId, NotificationType.AppointmentUpdated, "The customer confirmed",
            "The customer confirmed the appointment for \"" + serviceRequest.Title + "\" at " + AppointmentRules.FormatLocal(appointment.StartAt) + ".",
            "Appointment", appointment.Id, ct);
        await db.SaveChangesAsync(ct);

        return await GetAsync(id, ct);
    }

    public async Task<CustomerAppointmentDto> CancelAsync(Guid id, CancellationToken ct = default)
    {
        var customerId = CustomerId;
        var appointment = await FindAsync(id, customerId, ct);
        var serviceRequest = await db.ServiceRequests.FirstAsync(r => r.Id == appointment.ServiceRequestId, ct);

        var before = Snapshot(appointment);
        appointment.Cancel();              // 422 unless it is Scheduled or Confirmed
        serviceRequest.ReturnToAccepted(); // the provider can propose a new time

        audit.Record("AppointmentCancelled", nameof(Appointment), appointment.Id, before, Snapshot(appointment));
        await notifier.ToProviderAsync(
            appointment.ProviderId, NotificationType.AppointmentUpdated, "Appointment cancelled",
            "The customer cancelled the appointment for \"" + serviceRequest.Title + "\". You can propose a new time.",
            "ServiceRequest", appointment.ServiceRequestId, ct);
        await db.SaveChangesAsync(ct);

        return await GetAsync(id, ct);
    }

    private async Task<Appointment> FindAsync(Guid id, Guid customerId, CancellationToken ct) =>
        await db.Appointments.FirstOrDefaultAsync(a => a.Id == id && a.CustomerId == customerId, ct)
        ?? throw new NotFoundException("Appointment not found.");

    private static CustomerAppointmentDto ToDto(AppointmentProjections.Row r) => new(
        r.Id, r.RequestId, r.RequestTitle, r.ServiceName, r.StartAt, r.EndAt, r.Address, r.Notes, r.Status,
        r.ProviderId, r.ProviderName, r.ProviderPhone, r.Amount, r.CreatedAt);

    // The audit trail records the workflow, never anyone's address or phone number.
    private static object Snapshot(Appointment a) => new { a.ServiceRequestId, a.Status, a.StartAt, a.EndAt };
}