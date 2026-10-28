using EthioServe.Application.Abstractions;
using EthioServe.Application.Common;
using EthioServe.Application.Common.Exceptions;
using EthioServe.Application.Notifications;
using EthioServe.Application.Requests;
using EthioServe.Domain.Common;
using EthioServe.Domain.Entities;
using EthioServe.Domain.Enums;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace EthioServe.Application.Quotes;

public sealed class ProviderQuoteService(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    IAuditTrail audit,
    IClock clock,
    IProviderFeedService feed,
    INotifier notifier,
    IValidator<SubmitQuoteRequest> validator) : IProviderQuoteService
{
    private const int MaxAttempts = 3;

    private sealed class Row
    {
        public Guid Id { get; set; }

        public Guid RequestId { get; set; }

        public string RequestTitle { get; set; } = string.Empty;

        public string ServiceName { get; set; } = string.Empty;

        public string CityName { get; set; } = string.Empty;

        public ServiceRequestStatus RequestStatus { get; set; }

        public decimal Amount { get; set; }

        public string Description { get; set; } = string.Empty;

        public int EstimatedDurationMinutes { get; set; }

        public DateTimeOffset ValidUntil { get; set; }

        public QuoteStatus Status { get; set; }

        public DateTimeOffset CreatedAt { get; set; }

        public string? ContactName { get; set; }

        public string? ContactPhone { get; set; }

        public string? ContactAddress { get; set; }
    }

    // The provider comes from the signed-in user's token, never from the request.
    private Guid ProviderId =>
        currentUser.ProviderId ?? throw new ForbiddenException("This account has no provider profile.");

    public async Task<ProviderQuoteDto> SubmitAsync(Guid requestId, SubmitQuoteRequest request, CancellationToken ct = default)
    {
        var providerId = ProviderId;
        await validator.ValidateAndThrowAsync(request, ct);

        // The same rule as the feed: a verified provider, an open request, their city, a service they offer.
        // Anything else is a 403 or a 404.
        _ = await feed.GetAsync(requestId, ct);

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var id = await TrySubmitAsync(providerId, requestId, request, ct);

                return await GetAsync(id, ct);
            }
            catch (DbUpdateConcurrencyException) when (attempt < MaxAttempts)
            {
                // Another provider quoted first and moved the request on. Start again from what is stored now.
                db.DiscardChanges();
            }
        }
    }

    public async Task<PagedResult<ProviderQuoteDto>> ListAsync(ProviderQuoteQuery query, CancellationToken ct = default)
    {
        var providerId = ProviderId;
        var (page, pageSize) = Paging.Normalize(query.Page, query.PageSize);

        var rows = Rows(providerId);

        if (query.RequestId is { } requestId)
        {
            rows = rows.Where(x => x.RequestId == requestId);
        }
        var total = await rows.CountAsync(ct);

        var pageRows = await rows
            .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var now = clock.UtcNow;

        return new PagedResult<ProviderQuoteDto>(pageRows.Select(r => ToDto(r, now)).ToList(), page, pageSize, total);
    }

    // Looked up by id AND owner: another provider's quote is simply "not found".
    public async Task<ProviderQuoteDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var providerId = ProviderId;

        var row = await Rows(providerId).Where(x => x.Id == id).FirstOrDefaultAsync(ct)
                  ?? throw new NotFoundException("Quote not found.");

        return ToDto(row, clock.UtcNow);
    }

    public async Task<ProviderQuoteDto> WithdrawAsync(Guid id, CancellationToken ct = default)
    {
        var providerId = ProviderId;
        var now = clock.UtcNow;

        var quote = await db.Quotes.FirstOrDefaultAsync(q => q.Id == id && q.ProviderId == providerId, ct)
                    ?? throw new NotFoundException("Quote not found.");

        var before = Snapshot(quote);
        quote.Withdraw(); // 422 unless it is still pending

        var serviceRequest = await db.ServiceRequests.FirstOrDefaultAsync(r => r.Id == quote.ServiceRequestId, ct);

        if (serviceRequest is { Status: ServiceRequestStatus.Quoted })
        {
            var anotherIsOpen = await db.Quotes.AnyAsync(
                q => q.ServiceRequestId == quote.ServiceRequestId
                     && q.Id != quote.Id
                     && q.Status == QuoteStatus.Pending
                     && q.ValidUntil > now,
                ct);

            if (!anotherIsOpen)
            {
                serviceRequest.ReopenForQuotes();
            }
        }

        audit.Record("QuoteWithdrawn", nameof(Quote), quote.Id, before, Snapshot(quote));
        await db.SaveChangesAsync(ct);

        return await GetAsync(id, ct);
    }

    private async Task<Guid> TrySubmitAsync(Guid providerId, Guid requestId, SubmitQuoteRequest input, CancellationToken ct)
    {
        var now = clock.UtcNow;

        var serviceRequest = await db.ServiceRequests.FirstOrDefaultAsync(r => r.Id == requestId, ct)
                             ?? throw new NotFoundException("Request not found.");

        if (serviceRequest.Status is not (ServiceRequestStatus.Pending or ServiceRequestStatus.Quoted))
        {
            throw new DomainException("This request is no longer open for quotes.");
        }

        var mine = await db.Quotes.FirstOrDefaultAsync(
            q => q.ServiceRequestId == requestId && q.ProviderId == providerId && q.Status == QuoteStatus.Pending, ct);

        if (mine is not null)
        {
            if (mine.IsOpen(now))
            {
                throw new ConflictException(
                    "You already sent a quote for this request. Withdraw it first if you want to send a new one.");
            }

            // A quote past its deadline must not block a new one (one open quote per provider per request).
            var before = Snapshot(mine);
            mine.Expire(now);
            audit.Record("QuoteExpired", nameof(Quote), mine.Id, before, Snapshot(mine));
            await db.SaveChangesAsync(ct);
        }

        var quote = new Quote(
            requestId, providerId, input.Amount, input.Description, input.EstimatedDurationMinutes,
            now.AddDays(input.ValidForDays));

        db.Quotes.Add(quote);

        if (serviceRequest.Status == ServiceRequestStatus.Pending)
        {
            serviceRequest.MarkQuoted();
        }

        audit.Record("QuoteSubmitted", nameof(Quote), quote.Id, null, Snapshot(quote));

        var providerName = await db.ProviderProfiles.AsNoTracking()
            .Where(p => p.Id == providerId)
            .Select(p => p.BusinessName)
            .FirstAsync(ct);

        await notifier.ToCustomerAsync(
            serviceRequest.CustomerId, NotificationType.QuoteReceived, "New quote received",
            providerName + " sent a quote of " + quote.Amount.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)
            + " ETB for \"" + serviceRequest.Title + "\".",
            "ServiceRequest", serviceRequest.Id, ct);
        await db.SaveChangesAsync(ct);

        return quote.Id;
    }

    // Contact details are selected only for an accepted quote, so they are never even read for any other.
    private IQueryable<Row> Rows(Guid providerId) =>
        from q in db.Quotes.AsNoTracking()
        join r in db.ServiceRequests.AsNoTracking() on q.ServiceRequestId equals r.Id
        join s in db.Services.AsNoTracking() on r.ServiceId equals s.Id
        join c in db.Cities.AsNoTracking() on r.CityId equals c.Id
        join customer in db.CustomerProfiles.AsNoTracking() on r.CustomerId equals customer.Id
        where q.ProviderId == providerId
        select new Row
        {
            Id = q.Id,
            RequestId = r.Id,
            RequestTitle = r.Title,
            ServiceName = s.Name,
            CityName = c.Name,
            RequestStatus = r.Status,
            Amount = q.Amount,
            Description = q.Description,
            EstimatedDurationMinutes = q.EstimatedDurationMinutes,
            ValidUntil = q.ValidUntil,
            Status = q.Status,
            CreatedAt = q.CreatedAt,
            ContactName = q.Status == QuoteStatus.Accepted ? customer.FirstName + " " + customer.LastName : null,
            ContactPhone = q.Status == QuoteStatus.Accepted ? customer.PhoneNumber : null,
            ContactAddress = q.Status == QuoteStatus.Accepted ? r.Address : null
        };

    private static ProviderQuoteDto ToDto(Row r, DateTimeOffset now) => new(
        r.Id, r.RequestId, r.RequestTitle, r.ServiceName, r.CityName, r.RequestStatus, r.Amount, r.Description,
        r.EstimatedDurationMinutes, r.ValidUntil,
        QuoteStatusRules.Effective(r.Status, r.ValidUntil, r.RequestStatus, now),
        r.CreatedAt,
        r.Status == QuoteStatus.Accepted && r.ContactName is not null && r.ContactPhone is not null && r.ContactAddress is not null
            ? new CustomerContactDto(r.ContactName, r.ContactPhone, r.ContactAddress)
            : null);

    // The audit trail records the workflow, never anyone's contact details.
    private static object Snapshot(Quote q) => new { q.ServiceRequestId, q.Status, q.Amount, q.ValidUntil };
}