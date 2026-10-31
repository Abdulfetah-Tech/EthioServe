using EthioServe.Application.Common;
using EthioServe.Domain.Enums;

namespace EthioServe.Application.Payments;

public sealed record CreatePaymentRequest(
    Guid QuoteId,
    PaymentMethod PaymentMethod,
    string? IdempotencyKey = null);

public sealed record PaymentDto(
    Guid Id,
    Guid ServiceRequestId,
    Guid QuoteId,
    Guid CustomerId,
    Guid ProviderId,
    decimal Amount,
    string Currency,
    PaymentMethod PaymentMethod,
    PaymentStatus Status,
    string TransactionReference,
    string? IdempotencyKey,
    DateTimeOffset? PaidAt);

public sealed class PaymentQuery
{
    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = Paging.DefaultPageSize;
}

/// <summary>Payments for accepted quotes. Only the customer or the quoting provider can view a payment.</summary>
public interface IPaymentService
{
    Task<PaymentDto> CreateAsync(Guid serviceRequestId, CreatePaymentRequest request, CancellationToken ct = default);

    Task<PagedResult<PaymentDto>> ListAsync(Guid serviceRequestId, PaymentQuery query, CancellationToken ct = default);

    Task<PaymentDto> GetAsync(Guid id, CancellationToken ct = default);
}
