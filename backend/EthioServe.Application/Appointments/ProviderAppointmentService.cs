using EthioServe.Application.Abstractions;
using EthioServe.Application.Common;
using EthioServe.Application.Common.Exceptions;
using EthioServe.Application.Notifications;
using EthioServe.Domain.Common;
using EthioServe.Domain.Entities;
using EthioServe.Domain.Enums;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;

namespace EthioServe.Application.Appointments;

public sealed class ProviderAppointmentService(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    IAuditTrail audit,
    IClock clock,
    INotifier notifier,
    IValidator<ScheduleAppointmentRequest> scheduleValidator,
    IValidator<RescheduleAppointmentRequest> rescheduleValidator) : IProviderAppointmentService
{
    // The provider comes from the signed-in user's token, never from the request.
    private Guid ProviderId =>
        currentUser.ProviderId ?? throw new ForbiddenException("This account has no provider profile.");

    public async Task<ProviderAppointmentDto> ScheduleAsync(Guid requestId, ScheduleAppointmentRequest request, CancellationToken ct = default)
    {
        var providerId = ProviderId;
        await scheduleValidator.ValidateAndThrowAsync(request, ct);

        // Matched on the request, the accepted quote AND this provider: a job that is not yours is simply "not found".
        var quote = await db.Quotes.AsNoTracking().FirstOrDefaultAsync(
                        q => q.ServiceRequestId == requestId && q.ProviderId == providerId && q.Status == QuoteStatus.Accepted, ct)
                    ?? throw new NotFoundException("Request not found.");

        var serviceRequest = await db.ServiceRequests.FirstAsync(r => r.Id == requestId, ct);

        if (serviceRequest.Status != ServiceRequestStatus.Accepted)
        {
            throw new DomainException("This job already has an appointment, or is no longer waiting for one.");
        }

        var start = AppointmentRules.Normalize(request.StartAt);

        if (AppointmentRules.StartProblem(start, clock.UtcNow) is { } problem)
        {
            throw Invalid(problem);
        }

        var end = AppointmentRules.EndFor(start, quote.EstimatedDurationMinutes);
        await EnsureFreeAsync(providerId, start, end, Guid.Empty, ct);

        var appointment = new Appointment(
            requestId, quote.Id, providerId, serviceRequest.CustomerId, start, end, serviceRequest.Address, Clean(request.Notes));

        serviceRequest.MarkScheduled();
        db.Appointments.Add(appointment);
        audit.Record("AppointmentScheduled", nameof(Appointment), appointment.Id, null, Snapshot(appointment));
        await notifier.ToCustomerAsync(
            serviceRequest.CustomerId, NotificationType.AppointmentUpdated, "A time was proposed",
            "A time was proposed for \"" + serviceRequest.Title + "\": " + AppointmentRules.FormatLocal(appointment.StartAt) + " to " + AppointmentRules.FormatLocal(appointment.EndAt) + " (Addis Ababa time).",
            "ServiceRequest", serviceRequest.Id, ct);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("That time was just taken by another appointment. Please choose a different time.");
        }
        catch (DbUpdateException)
        {
            throw new ConflictException("That time was just taken by another appointment. Please choose a different time.");
        }

        return await GetAsync(appointment.Id, ct);
    }

    public async Task<PagedResult<ProviderAppointmentDto>> ListAsync(AppointmentQuery query, CancellationToken ct = default)
    {
        var providerId = ProviderId;
        var (page, pageSize) = Paging.Normalize(query.Page, query.PageSize);

        var rows = AppointmentProjections.Rows(db).Where(x => x.ProviderId == providerId);

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

        return new PagedResult<ProviderAppointmentDto>(pageRows.Select(ToDto).ToList(), page, pageSize, total);
    }

    // Looked up by id AND owner: another provider's appointment is simply "not found".
    public async Task<ProviderAppointmentDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var providerId = ProviderId;

        var row = await AppointmentProjections.Rows(db)
                      .Where(x => x.Id == id && x.ProviderId == providerId)
                      .FirstOrDefaultAsync(ct)
                  ?? throw new NotFoundException("Appointment not found.");

        return ToDto(row);
    }

public async Task<ProviderAppointmentDto> RescheduleAsync(Guid id, RescheduleAppointmentRequest request, CancellationToken ct = default)
    {
        var providerId = ProviderId;
        await rescheduleValidator.ValidateAndThrowAsync(request, ct);

        var appointment = await FindAsync(id, providerId, ct);
        var serviceRequest = await db.ServiceRequests.FirstAsync(r => r.Id == appointment.ServiceRequestId, ct);
        var start = AppointmentRules.Normalize(request.StartAt);

        if (AppointmentRules.StartProblem(start, clock.UtcNow) is { } problem)
        {
            throw Invalid(problem);
        }

        var minutes = await db.Quotes.AsNoTracking()
            .Where(q => q.Id == appointment.QuoteId)
            .Select(q => q.EstimatedDurationMinutes)
            .FirstAsync(ct);

        var before = Snapshot(appointment);
        var end = AppointmentRules.EndFor(start, minutes);

        appointment.Reschedule(start, end, Clean(request.Notes)); // 422 unless it is Scheduled or Confirmed
        await EnsureFreeAsync(providerId, start, end, id, ct);

        audit.Record("AppointmentRescheduled", nameof(Appointment), appointment.Id, before, Snapshot(appointment));
        await notifier.ToCustomerAsync(
            appointment.CustomerId, NotificationType.AppointmentUpdated, "The time was changed",
            "The time for \"" + serviceRequest.Title + "\" was changed to " + AppointmentRules.FormatLocal(appointment.StartAt) + " to " + AppointmentRules.FormatLocal(appointment.EndAt) + " (Addis Ababa time).",
            "ServiceRequest", serviceRequest.Id, ct);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("That time was just taken by another appointment. Please choose a different time.");
        }
        catch (DbUpdateException)
        {
            throw new ConflictException("That time was just taken by another appointment. Please choose a different time.");
        }

        return await GetAsync(id, ct);
    }

    public async Task<ProviderAppointmentDto> CancelAsync(Guid id, CancellationToken ct = default)
    {
        var providerId = ProviderId;
        var appointment = await FindAsync(id, providerId, ct);
        var serviceRequest = await db.ServiceRequests.FirstAsync(r => r.Id == appointment.ServiceRequestId, ct);

var before = Snapshot(appointment);
        appointment.Cancel();            // 422 unless it is Scheduled or Confirmed
        serviceRequest.ReturnToAccepted(); // the job waits for a new time

        audit.Record("AppointmentCancelled", nameof(Appointment), appointment.Id, before, Snapshot(appointment));
        await notifier.ToCustomerAsync(
            appointment.CustomerId, NotificationType.AppointmentUpdated, "Appointment cancelled",
            "The provider cancelled the appointment for \"" + serviceRequest.Title + "\". You can accept another quote and choose a new time.",
            "ServiceRequest", appointment.ServiceRequestId, ct);
        await db.SaveChangesAsync(ct);

        return await GetAsync(id, ct);
    }

    public async Task<ProviderAppointmentDto> StartAsync(Guid id, CancellationToken ct = default)
    {
        var providerId = ProviderId;
        var appointment = await FindAsync(id, providerId, ct);
        var serviceRequest = await db.ServiceRequests.FirstAsync(r => r.Id == appointment.ServiceRequestId, ct);

        var before = Snapshot(appointment);
        appointment.Start(); // 422 unless the customer confirmed it

        if (!AppointmentRules.CanStartWork(appointment.StartAt, clock.UtcNow))
        {
            throw new DomainException(
                $"You can start the job up to 2 hours before the agreed time ({AppointmentRules.FormatLocal(appointment.StartAt)}, Addis Ababa time).");
        }

serviceRequest.StartWork();

        audit.Record("AppointmentStarted", nameof(Appointment), appointment.Id, before, Snapshot(appointment));
        await notifier.ToCustomerAsync(
            appointment.CustomerId, NotificationType.AppointmentUpdated, "The job has started",
            "The provider started the job for \"" + serviceRequest.Title + "\". Keep your phone nearby.",
            "ServiceRequest", appointment.ServiceRequestId, ct);
        await db.SaveChangesAsync(ct);

        return await GetAsync(id, ct);
    }

    public async Task<ProviderAppointmentDto> CompleteAsync(Guid id, CancellationToken ct = default)
    {
        var providerId = ProviderId;
        var appointment = await FindAsync(id, providerId, ct);
        var serviceRequest = await db.ServiceRequests.FirstAsync(r => r.Id == appointment.ServiceRequestId, ct);

        var before = Snapshot(appointment);
        appointment.Complete(); // 422 unless the job is in progress
serviceRequest.Complete(clock.UtcNow);

        audit.Record("AppointmentCompleted", nameof(Appointment), appointment.Id, before, Snapshot(appointment));
        await notifier.ToCustomerAsync(
            appointment.CustomerId, NotificationType.AppointmentUpdated, "The job is complete",
            "The provider marked the job for \"" + serviceRequest.Title + "\" as complete. Please leave a review.",
            "ServiceRequest", appointment.ServiceRequestId, ct);
        await db.SaveChangesAsync(ct);

        return await GetAsync(id, ct);
    }

    private async Task<Appointment> FindAsync(Guid id, Guid providerId, CancellationToken ct) =>
        await db.Appointments.FirstOrDefaultAsync(a => a.Id == id && a.ProviderId == providerId, ct)
        ?? throw new NotFoundException("Appointment not found.");

    /// <summary>
    /// The friendly check. The database enforces the same rule, so two requests arriving at the same moment cannot
    /// both succeed even though both pass this check.
    /// </summary>
    private async Task EnsureFreeAsync(Guid providerId, DateTimeOffset start, DateTimeOffset end, Guid exceptId, CancellationToken ct)
    {
        var clash = await db.Appointments.AsNoTracking()
            .Where(a => a.ProviderId == providerId
                        && a.Id != exceptId
                        && (a.Status == AppointmentStatus.Scheduled
                            || a.Status == AppointmentStatus.Confirmed
                            || a.Status == AppointmentStatus.InProgress)
                        && a.StartAt < end
                        && a.EndAt > start)
            .OrderBy(a => a.StartAt)
            .Select(a => new { a.StartAt, a.EndAt })
            .FirstOrDefaultAsync(ct);

        if (clash is not null)
        {
            throw new ConflictException(
                $"You already have an appointment from {AppointmentRules.FormatLocal(clash.StartAt)} to {AppointmentRules.FormatLocal(clash.EndAt)} (Addis Ababa time). Choose a time that does not overlap.");
        }
    }

    private static ValidationException Invalid(string message) =>
        new(new[] { new ValidationFailure("StartAt", message) });

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static ProviderAppointmentDto ToDto(AppointmentProjections.Row r) => new(
        r.Id, r.RequestId, r.RequestTitle, r.ServiceName, r.CityName, r.StartAt, r.EndAt, r.Address, r.Notes,
        r.Status, r.CustomerName, r.CustomerPhone, r.Amount, r.CreatedAt);

    // The audit trail records the workflow, never anyone's address or phone number.
    private static object Snapshot(Appointment a) => new { a.ServiceRequestId, a.Status, a.StartAt, a.EndAt };
}
