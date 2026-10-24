using EthioServe.Application.Abstractions;
using EthioServe.Application.Common;
using EthioServe.Application.Common.Exceptions;
using EthioServe.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EthioServe.Application.Requests;

public sealed class ProviderFeedService(IApplicationDbContext db, ICurrentUser currentUser) : IProviderFeedService
{
    public async Task<PagedResult<FeedItemDto>> ListAsync(FeedQuery query, CancellationToken ct = default)
    {
        var (page, pageSize) = Paging.Normalize(query.Page, query.PageSize);

        var rows = await VisibleRowsAsync(ct);

        if (query.ServiceId is { } serviceId)
        {
            rows = rows.Where(x => x.Request.ServiceId == serviceId);
        }

        var total = await rows.CountAsync(ct);

        var pageRows = await rows
            .OrderByDescending(x => x.Request.CreatedAt).ThenByDescending(x => x.Request.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<FeedItemDto>(pageRows.Select(RequestProjections.ToFeedItem).ToList(), page, pageSize, total);
    }

    // A request outside the provider's feed is "not found", so a provider cannot probe other requests.
    public async Task<FeedDetailDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var rows = await VisibleRowsAsync(ct);

        var row = await rows.Where(x => x.Request.Id == id).FirstOrDefaultAsync(ct)
                  ?? throw new NotFoundException("Request not found.");

        return RequestProjections.ToFeedDetail(row);
    }

    /// <summary>
    /// The requests this provider may see: open, in the provider's city, for a service the provider actively offers,
    /// with the service and its category still active. Only verified providers see any.
    /// </summary>
    private async Task<IQueryable<RequestProjections.RequestRow>> VisibleRowsAsync(CancellationToken ct)
    {
        var providerId = currentUser.ProviderId ?? throw new ForbiddenException("This account has no provider profile.");

        var provider = await db.ProviderProfiles.AsNoTracking()
                           .Where(p => p.Id == providerId)
                           .Select(p => new { p.VerificationStatus, p.CityId })
                           .FirstOrDefaultAsync(ct)
                       ?? throw new NotFoundException("Provider profile not found.");

        if (provider.VerificationStatus != VerificationStatus.Verified)
        {
            throw new ForbiddenException("Your profile must be verified before you can see customer requests.");
        }

        var cityId = provider.CityId;
        var myServices = db.ProviderServices.AsNoTracking()
            .Where(o => o.ProviderId == providerId && o.IsActive)
            .Select(o => o.ServiceId);

        return RequestProjections.Rows(db).Where(x =>
            x.Request.CityId == cityId
            && (x.Request.Status == ServiceRequestStatus.Pending || x.Request.Status == ServiceRequestStatus.Quoted)
            && x.ServiceIsActive
            && x.CategoryIsActive
            && myServices.Contains(x.Request.ServiceId));
    }
}