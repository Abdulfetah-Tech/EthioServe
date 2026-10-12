using EthioServe.Domain.Common;
using EthioServe.Domain.Enums;

namespace EthioServe.Domain.Entities;

public sealed class Quote : AuditableEntity
{
    private Quote() { }

    public Quote(
        Guid serviceRequestId, Guid providerId, decimal amount, string description,
        int estimatedDurationMinutes, DateTimeOffset validUntil)
    {
        if (estimatedDurationMinutes <= 0)
        {
            throw new DomainException("estimatedDurationMinutes must be greater than zero.");
        }

        ServiceRequestId = serviceRequestId;
        ProviderId = providerId;
        Amount = Guard.Positive(amount, nameof(amount));
        Description = Guard.NotBlank(description, nameof(description));
        EstimatedDurationMinutes = estimatedDurationMinutes;
        ValidUntil = validUntil;
        Status = QuoteStatus.Pending;
    }

    public Guid ServiceRequestId { get; private set; }

    public Guid ProviderId { get; private set; }

    public decimal Amount { get; private set; }

    public string Description { get; private set; } = string.Empty;

    public int EstimatedDurationMinutes { get; private set; }

    public DateTimeOffset ValidUntil { get; private set; }

    public QuoteStatus Status { get; private set; }

    public uint Version { get; private set; }
}