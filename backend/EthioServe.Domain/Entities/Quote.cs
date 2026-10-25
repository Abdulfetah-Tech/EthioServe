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

    /// <summary>Changed only through the methods below. Only a pending quote can change.</summary>
    public QuoteStatus Status { get; private set; }

    public uint Version { get; private set; }

    /// <summary>Pending and not past its deadline: the customer can still accept it.</summary>
    public bool IsOpen(DateTimeOffset now) => Status == QuoteStatus.Pending && ValidUntil > now;

    public void Accept(DateTimeOffset now)
    {
        RequirePending("accepted");

        if (ValidUntil <= now)
        {
            throw new DomainException("This quote has expired.");
        }

        Status = QuoteStatus.Accepted;
    }

    /// <summary>The customer declined it, or chose another quote.</summary>
    public void Reject()
    {
        RequirePending("rejected");
        Status = QuoteStatus.Rejected;
    }

    /// <summary>The provider took it back.</summary>
    public void Withdraw()
    {
        RequirePending("withdrawn");
        Status = QuoteStatus.Cancelled;
    }

    public void Expire(DateTimeOffset now)
    {
        RequirePending("expired");

        if (ValidUntil > now)
        {
            throw new DomainException("This quote is still valid.");
        }

        Status = QuoteStatus.Expired;
    }

    private void RequirePending(string action)
    {
        if (Status != QuoteStatus.Pending)
        {
            throw new DomainException($"A quote that is {Status} cannot be {action}.");
        }
    }
}