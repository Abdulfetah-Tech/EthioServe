using EthioServe.Application.Abstractions;
using EthioServe.Application.Common;
using EthioServe.Application.Common.Exceptions;
using EthioServe.Application.Notifications;
using EthioServe.Domain.Entities;
using EthioServe.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EthioServe.Application.Quotes;

public sealed class CustomerQuoteService(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    IAuditTrail audit,
    IClock clock,
    INotifier notifier) : ICustomerQuoteService
{
    private sealed class Row
    {
        public Guid Id { get; set; }

        public Guid ProviderId { get; set; }

        public string ProviderName { get; set; } = string.Empty;

        public decimal ProviderRating { get; set; }

        public int ProviderReviews { get; set; }

        public int ProviderYears { get; set; }

        public decimal Amount { get; set; }

        public string Description { get; set; } = string.Empty;

        public int EstimatedDurationMinutes { get; set; }

        public DateTimeOffset ValidUntil { get; set; }

        public QuoteStatus Status { get; set; }

        public ServiceRequestStatus RequestStatus { get; set; }

        public DateTimeOffset CreatedAt { get; set; }

        public string? ProviderPhone { get; set; }
    }

    // The customer comes from the signed-in user's token, never from the request.
    private Guid CustomerId =>
        currentUser.CustomerId ?? throw new ForbiddenException("This account has no customer profile.");

    public async Task<IReadOnlyList<QuoteDto>> ListAsync(Guid requestId, CancellationToken ct = default)
    {
        var customerId = CustomerId;
        _ = await FindRequestAsync(requestId, customerId, ct);

        // Cheapest first: this is the list the customer compares. Quotes a provider withdrew are not shown.
        var rows = await Rows(requestId)
            .OrderBy(x => x.Amount).ThenBy(x => x.CreatedAt).ThenBy(x => x.Id)
            .ToListAsync(ct);

        var now = clock.UtcNow;

        return rows.Select(r => ToDto(r, now)).ToList();
    }

    /// <summary>
    /// One transaction: the chosen quote is accepted, every other open quote is rejected, and the request is
    /// accepted. Two people choosing at once cannot both win: the request and quote rows carry an xmin concurrency
    /// check, and the database allows only one accepted quote per request.
    /// </summary>
    public async Task<QuoteDto> AcceptAsync(Guid requestId, Guid quoteId, CancellationToken ct = default)
    {
        var customerId = CustomerId;
        var now = clock.UtcNow;

        var serviceRequest = await FindRequestAsync(requestId, customerId, ct);

        // Ordered by id, so concurrent accepts lock the same rows in the same order.
        var quotes = await db.Quotes
            .Where(q => q.ServiceRequestId == requestId && (q.Id == quoteId || q.Status == QuoteStatus.Pending))
            .OrderBy(q => q.Id)
            .ToListAsync(ct);

        var chosen = quotes.FirstOrDefault(q => q.Id == quoteId) ?? throw new NotFoundException("Quote not found.");

        var before = Snapshot(chosen);
        chosen.Accept(now);           // 422 when it is not open or has expired
        serviceRequest.AcceptQuote(); // 422 when the request is closed

        audit.Record("QuoteAccepted", nameof(Quote), chosen.Id, before, Snapshot(chosen));
        await notifier.ToProviderAsync(
            chosen.ProviderId, NotificationType.QuoteAccepted, "Your quote was accepted",
            "The customer accepted your quote for \"" + serviceRequest.Title + "\". Open the quote to book a time.",
            "Quote", chosen.Id, ct);

        foreach (var other in quotes.Where(q => q.Id != quoteId))
        {
            var otherBefore = Snapshot(other);
            other.Reject();
            audit.Record("QuoteRejected", nameof(Quote), other.Id, otherBefore, Snapshot(other));
            await notifier.ToProviderAsync(
                other.ProviderId, NotificationType.General, "Quote not chosen",
                "The customer chose another quote for \"" + serviceRequest.Title + "\".",
                "Quote", other.Id, ct);
        }

        await db.SaveChangesAsync(ct);

        return await GetOneAsync(requestId, quoteId, ct);
    }

    public async Task<QuoteDto> DeclineAsync(Guid requestId, Guid quoteId, CancellationToken ct = default)
    {
        var customerId = CustomerId;
        var now = clock.UtcNow;

        var serviceRequest = await FindRequestAsync(requestId, customerId, ct);

        var quote = await db.Quotes.FirstOrDefaultAsync(q => q.Id == quoteId && q.ServiceRequestId == requestId, ct)
                    ?? throw new NotFoundException("Quote not found.");

        var before = Snapshot(quote);
        quote.Reject(); // 422 unless it is still pending

        if (serviceRequest.Status == ServiceRequestStatus.Quoted)
        {
            var anotherIsOpen = await db.Quotes.AnyAsync(
                q => q.ServiceRequestId == requestId
                     && q.Id != quoteId
                     && q.Status == QuoteStatus.Pending
                     && q.ValidUntil > now,
                ct);

            if (!anotherIsOpen)
            {
                serviceRequest.ReopenForQuotes();
            }
        }

        audit.Record("QuoteDeclined", nameof(Quote), quote.Id, before, Snapshot(quote));
        await notifier.ToProviderAsync(
            quote.ProviderId, NotificationType.General, "Quote declined",
            "The customer declined your quote for \"" + serviceRequest.Title + "\".",
            "Quote", quote.Id, ct);
        await db.SaveChangesAsync(ct);

        return await GetOneAsync(requestId, quoteId, ct);
    }

    // Looked up by id AND owner: another customer's request is simply "not found".
    private async Task<ServiceRequest> FindRequestAsync(Guid requestId, Guid customerId, CancellationToken ct) =>
        await db.ServiceRequests.FirstOrDefaultAsync(r => r.Id == requestId && r.CustomerId == customerId, ct)
        ?? throw new NotFoundException("Request not found.");

    private async Task<QuoteDto> GetOneAsync(Guid requestId, Guid quoteId, CancellationToken ct)
    {
        var row = await Rows(requestId).Where(x => x.Id == quoteId).FirstAsync(ct);

        return ToDto(row, clock.UtcNow);
    }

    // The provider's phone number is selected only for the accepted quote.
    private IQueryable<Row> Rows(Guid requestId) =>
        from q in db.Quotes.AsNoTracking()
        join p in db.ProviderProfiles.AsNoTracking() on q.ProviderId equals p.Id
        join r in db.ServiceRequests.AsNoTracking() on q.ServiceRequestId equals r.Id
        where q.ServiceRequestId == requestId && q.Status != QuoteStatus.Cancelled
        select new Row
        {
            Id = q.Id,
            ProviderId = p.Id,
            ProviderName = p.BusinessName,
            ProviderRating = p.AverageRating,
            ProviderReviews = p.TotalReviews,
            ProviderYears = p.YearsOfExperience,
            Amount = q.Amount,
            Description = q.Description,
            EstimatedDurationMinutes = q.EstimatedDurationMinutes,
            ValidUntil = q.ValidUntil,
            Status = q.Status,
            RequestStatus = r.Status,
            CreatedAt = q.CreatedAt,
            ProviderPhone = q.Status == QuoteStatus.Accepted ? p.PhoneNumber : null
        };

    private static QuoteDto ToDto(Row r, DateTimeOffset now) => new(
        r.Id, r.ProviderId, r.ProviderName, r.ProviderRating, r.ProviderReviews, r.ProviderYears, r.Amount,
        r.Description, r.EstimatedDurationMinutes, r.ValidUntil,
        QuoteStatusRules.Effective(r.Status, r.ValidUntil, r.RequestStatus, now),
        r.CreatedAt, r.ProviderPhone);

    // The audit trail records the workflow, never anyone's contact details.
    private static object Snapshot(Quote q) => new { q.ServiceRequestId, q.Status, q.Amount, q.ValidUntil };
}