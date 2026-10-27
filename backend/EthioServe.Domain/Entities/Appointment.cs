using EthioServe.Domain.Common;
using EthioServe.Domain.Enums;

namespace EthioServe.Domain.Entities;

public sealed class Appointment : AuditableEntity
{
    private Appointment() { }

    public Appointment(
        Guid serviceRequestId, Guid quoteId, Guid providerId, Guid customerId,
        DateTimeOffset startAt, DateTimeOffset endAt, string address, string? notes = null)
    {
        if (endAt <= startAt)
        {
            throw new DomainException("endAt must be after startAt.");
        }

        ServiceRequestId = serviceRequestId;
        QuoteId = quoteId;
        ProviderId = providerId;
        CustomerId = customerId;
        StartAt = startAt;
        EndAt = endAt;
        Address = Guard.NotBlank(address, nameof(address));
        Notes = notes;
        Status = AppointmentStatus.Scheduled;
    }

    public Guid ServiceRequestId { get; private set; }

    public Guid QuoteId { get; private set; }

    public Guid ProviderId { get; private set; }

    public Guid CustomerId { get; private set; }

    public DateTimeOffset StartAt { get; private set; }

    public DateTimeOffset EndAt { get; private set; }

    public string Address { get; private set; } = string.Empty;

    /// <summary>Changed only through the methods below.</summary>
    public AppointmentStatus Status { get; private set; }

    public string? Notes { get; private set; }

    public uint Version { get; private set; }

    /// <summary>The customer agreed to the proposed time.</summary>
    public void Confirm()
    {
        Require(AppointmentStatus.Scheduled, "confirmed");
        Status = AppointmentStatus.Confirmed;
    }

    /// <summary>A new time. The customer has to agree again, so the appointment goes back to Scheduled.</summary>
    public void Reschedule(DateTimeOffset startAt, DateTimeOffset endAt, string? notes)
    {
        if (Status is not (AppointmentStatus.Scheduled or AppointmentStatus.Confirmed))
        {
            throw new DomainException($"An appointment that is {Status} cannot be rescheduled.");
        }

        if (endAt <= startAt)
        {
            throw new DomainException("endAt must be after startAt.");
        }

        StartAt = startAt;
        EndAt = endAt;
        Notes = notes;
        Status = AppointmentStatus.Scheduled;
    }

    public void Cancel()
    {
        if (Status is not (AppointmentStatus.Scheduled or AppointmentStatus.Confirmed))
        {
            throw new DomainException($"An appointment that is {Status} cannot be cancelled.");
        }

        Status = AppointmentStatus.Cancelled;
    }

    /// <summary>The provider began the job. Only a confirmed appointment can start.</summary>
    public void Start()
    {
        Require(AppointmentStatus.Confirmed, "started");
        Status = AppointmentStatus.InProgress;
    }

    public void Complete()
    {
        Require(AppointmentStatus.InProgress, "completed");
        Status = AppointmentStatus.Completed;
    }

    private void Require(AppointmentStatus expected, string action)
    {
        if (Status != expected)
        {
            throw new DomainException($"An appointment that is {Status} cannot be {action}.");
        }
    }
}