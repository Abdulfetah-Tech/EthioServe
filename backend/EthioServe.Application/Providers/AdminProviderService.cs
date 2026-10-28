using EthioServe.Application.Abstractions;
using EthioServe.Application.Common;
using EthioServe.Application.Common.Exceptions;
using EthioServe.Application.Notifications;
using EthioServe.Domain.Entities;
using EthioServe.Domain.Enums;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace EthioServe.Application.Providers;

public sealed class AdminProviderService(
    IApplicationDbContext db,
    IAuditTrail audit,
    IClock clock,
    INotifier notifier,
    IValidator<ReviewDecisionRequest> reasonValidator) : IAdminProviderService
{
    private const int MaxSearchLength = 100;

    public async Task<PagedResult<AdminProviderListItemDto>> SearchAsync(AdminProviderQuery query, CancellationToken ct = default)
    {
        var (page, pageSize) = Paging.Normalize(query.Page, query.PageSize);

        var providers = db.ProviderProfiles.AsNoTracking();

        if (query.Status is { } status)
        {
            providers = providers.Where(p => p.VerificationStatus == status);
        }

        var term = query.Search?.Trim();
        if (!string.IsNullOrEmpty(term))
        {
            var lowered = (term.Length > MaxSearchLength ? term[..MaxSearchLength] : term).ToLowerInvariant();
            providers = providers.Where(p => p.BusinessName.ToLower().Contains(lowered));
        }

        var total = await providers.CountAsync(ct);

        // Oldest first: this is a review queue.
        var items = await providers
            .OrderBy(p => p.CreatedAt).ThenBy(p => p.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new AdminProviderListItemDto(
                p.Id,
                p.BusinessName,
                p.PhoneNumber,
                db.Cities.Where(c => c.Id == p.CityId).Select(c => c.Name).FirstOrDefault() ?? string.Empty,
                p.VerificationStatus,
                p.VerificationNote,
                p.AverageRating,
                p.TotalReviews,
                db.ProviderServices.Count(o => o.ProviderId == p.Id && o.IsActive),
                p.CreatedAt,
                p.VerificationReviewedAt))
            .ToListAsync(ct);

        return new PagedResult<AdminProviderListItemDto>(items, page, pageSize, total);
    }

    public async Task<AdminProviderDetailDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var profile = await ProviderProjections.Profiles(db, db.ProviderProfiles.AsNoTracking().Where(p => p.Id == id))
                          .FirstOrDefaultAsync(ct)
                      ?? throw new NotFoundException("Provider not found.");

        var meta = await db.ProviderProfiles.AsNoTracking()
            .Where(p => p.Id == id)
            .Select(p => new { p.UserId, p.CreatedAt, p.VerificationReviewedAt })
            .FirstAsync(ct);

        var offers = await ProviderProjections.OfferRows(db)
            .Where(x => x.Offer.ProviderId == id)
            .OrderBy(x => x.CategoryName).ThenBy(x => x.ServiceName)
            .Select(ProviderProjections.ToOfferDto)
            .ToListAsync(ct);

        return new AdminProviderDetailDto(profile, meta.UserId, meta.CreatedAt, meta.VerificationReviewedAt, offers);
    }

    public Task<AdminProviderDetailDto> ApproveAsync(Guid id, CancellationToken ct = default) =>
        DecideAsync(id, "ProviderApproved", p => p.Approve(clock.UtcNow), ct);

    public async Task<AdminProviderDetailDto> RejectAsync(Guid id, ReviewDecisionRequest request, CancellationToken ct = default)
    {
        await reasonValidator.ValidateAndThrowAsync(request, ct);

        return await DecideAsync(id, "ProviderRejected", p => p.Reject(request.Reason, clock.UtcNow), ct);
    }

    public async Task<AdminProviderDetailDto> SuspendAsync(Guid id, ReviewDecisionRequest request, CancellationToken ct = default)
    {
        await reasonValidator.ValidateAndThrowAsync(request, ct);

        return await DecideAsync(id, "ProviderSuspended", p => p.Suspend(request.Reason, clock.UtcNow), ct);
    }

    public Task<AdminProviderDetailDto> ReinstateAsync(Guid id, CancellationToken ct = default) =>
        DecideAsync(id, "ProviderReinstated", p => p.Reinstate(clock.UtcNow), ct);

    private async Task<AdminProviderDetailDto> DecideAsync(
        Guid id, string action, Action<ProviderProfile> change, CancellationToken ct)
    {
        var provider = await db.ProviderProfiles.FirstOrDefaultAsync(p => p.Id == id, ct)
                       ?? throw new NotFoundException("Provider not found.");

        var before = Snapshot(provider);

        change(provider); // throws a DomainException (422) when the state machine does not allow this move

        audit.Record(action, nameof(ProviderProfile), provider.Id, before, Snapshot(provider));
        await NotifyDecisionAsync(provider, action, ct);
        await db.SaveChangesAsync(ct);

        return await GetAsync(id, ct);
    }

    private async Task NotifyDecisionAsync(ProviderProfile provider, string action, CancellationToken ct)
    {
        (string title, string message, NotificationType type)? decision = action switch
        {
            "ProviderApproved" => ("You are verified", "Your provider profile has been verified. You can now reply to customer requests.", NotificationType.ProviderVerified),
            "ProviderRejected" => ("Verification not approved", provider.VerificationNote ?? "Your verification was not approved.", NotificationType.General),
            "ProviderSuspended" => ("Your profile was suspended", provider.VerificationNote ?? "Your provider profile has been suspended.", NotificationType.General),
            "ProviderReinstated" => ("You are verified again", "Your provider profile has been reinstated.", NotificationType.ProviderVerified),
            _ => null
        };

        if (decision is { } d)
        {
            await notifier.ToProviderAsync(
                provider.Id, d.type, d.title, d.message, "ProviderProfile", provider.Id, ct);
        }
    }

    private static object Snapshot(ProviderProfile p) => new { p.VerificationStatus, p.VerificationNote };
}