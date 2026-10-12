using EthioServe.Domain.Common;
using EthioServe.Domain.Enums;

namespace EthioServe.Domain.Entities;

public sealed class Payment : BaseEntity
{
    public const string DefaultCurrency = "ETB";

    private Payment() { }

    public Payment(
        Guid serviceRequestId, Guid quoteId, Guid customerId, Guid providerId, decimal amount,
        PaymentMethod paymentMethod, string transactionReference, string? idempotencyKey = null)
    {
        ServiceRequestId = serviceRequestId;
        QuoteId = quoteId;
        CustomerId = customerId;
        ProviderId = providerId;
        Amount = Guard.Positive(amount, nameof(amount));
        Currency = DefaultCurrency;
        PaymentMethod = paymentMethod;
        TransactionReference = Guard.NotBlank(transactionReference, nameof(transactionReference));
        IdempotencyKey = idempotencyKey;
        Status = PaymentStatus.Pending;
    }

    public Guid ServiceRequestId { get; private set; }

    public Guid QuoteId { get; private set; }

    public Guid CustomerId { get; private set; }

    public Guid ProviderId { get; private set; }

    public decimal Amount { get; private set; }

    public string Currency { get; private set; } = DefaultCurrency;

    public PaymentMethod PaymentMethod { get; private set; }

    public PaymentStatus Status { get; private set; }

    public string TransactionReference { get; private set; } = string.Empty;

    public string? IdempotencyKey { get; private set; }

    public string? FailureReason { get; private set; }

    public DateTimeOffset? PaidAt { get; private set; }

    public uint Version { get; private set; }
}