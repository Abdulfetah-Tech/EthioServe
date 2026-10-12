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

    public AppointmentStatus Status { get; private set; }

    public string? Notes { get; private set; }

    public uint Version { get; private set; }
}