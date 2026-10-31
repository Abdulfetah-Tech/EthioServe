using EthioServe.Application.Abstractions;
using EthioServe.Application.Common;
using EthioServe.Application.Common.Exceptions;
using EthioServe.Domain.Entities;
using EthioServe.Domain.Enums;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace EthioServe.Application.Payments;

public sealed class CreatePaymentRequestValidator : AbstractValidator<CreatePaymentRequest>
{
    public CreatePaymentRequestValidator()
    {
        RuleFor(x => x.QuoteId).NotEmpty();
        RuleFor(x => x.PaymentMethod).IsInEnum();
        RuleFor(x => x.IdempotencyKey).MaximumLength(200);
    }
}

public sealed class PaymentService(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    IAuditTrail audit,
    IValidator<CreatePaymentRequest> validator) : IPaymentService
{
    private Guid CustomerId =>
        currentUser.CustomerId ?? throw new ForbiddenException("This account has no customer profile.");

    public async Task<PaymentDto> CreateAsync(
        Guid serviceRequestId, CreatePaymentRequest request, CancellationToken ct = default)
    {
        await validator.ValidateAndThrowAsync(request, ct);

        var customerId = CustomerId;

        var quote = await (
                from q in db.Quotes.AsNoTracking()
                join r in db.ServiceRequests.AsNoTracking() on q.ServiceRequestId equals r.Id
                where r.Id == serviceRequestId
                      && r.CustomerId == customerId
                      && q.Status == EthioServe.Domain.Enums.QuoteStatus.Accepted
                select q)
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Accepted quote not found for this request.");

        if (await db.Payments.AnyAsync(p => p.QuoteId == quote.Id && p.Status != PaymentStatus.Cancelled, ct))
        {
            throw new ConflictException("A payment already exists for this quote.");
        }

        var payment = new Payment(
            serviceRequestId, quote.Id, customerId, quote.ProviderId, quote.Amount,
            request.PaymentMethod, $"txn_{Guid.NewGuid():N}", request.IdempotencyKey);

        db.Payments.Add(payment);
        audit.Record(
            "PaymentCreated", nameof(Payment), payment.Id, null,
            new { payment.QuoteId, payment.Amount, payment.PaymentMethod });
        await db.SaveChangesAsync(ct);

        return ToDto(payment);
    }

    public async Task<PagedResult<PaymentDto>> ListAsync(
        Guid serviceRequestId, PaymentQuery query, CancellationToken ct = default)
    {
        var customerId = CustomerId;

        var ownsRequest = await db.ServiceRequests.AsNoTracking()
            .AnyAsync(r => r.Id == serviceRequestId && r.CustomerId == customerId, ct);

        if (!ownsRequest)
        {
            throw new NotFoundException("Request not found.");
        }

        var (page, pageSize) = Paging.Normalize(query.Page, query.PageSize);

        var rows = db.Payments.AsNoTracking()
            .Where(p => p.ServiceRequestId == serviceRequestId)
            .OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.Id);

        var total = await rows.CountAsync(ct);

        var items = await rows
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => ToDto(p))
            .ToListAsync(ct);

        return new PagedResult<PaymentDto>(items, page, pageSize, total);
    }

    public async Task<PaymentDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var customerId = CustomerId;

        var payment = await db.Payments.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id && p.CustomerId == customerId, ct)
            ?? throw new NotFoundException("Payment not found.");

        return ToDto(payment);
    }

    private static PaymentDto ToDto(Payment p) =>
        new(
            p.Id, p.ServiceRequestId, p.QuoteId, p.CustomerId, p.ProviderId,
            p.Amount, p.Currency, p.PaymentMethod, p.Status,
            p.TransactionReference, p.IdempotencyKey, p.PaidAt);
}
