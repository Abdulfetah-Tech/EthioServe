using EthioServe.Application.Abstractions;
using EthioServe.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EthioServe.Application.Requests;

/// <summary>One query shape for the customer and provider features, mapped to DTOs in memory after the page is fetched.</summary>
internal static class RequestProjections
{
    private const int SummaryLength = 160;

    internal sealed class RequestRow
    {
        public ServiceRequest Request { get; set; } = null!;

        public string ServiceName { get; set; } = string.Empty;

        public bool ServiceIsActive { get; set; }

        public Guid CategoryId { get; set; }

        public string CategoryName { get; set; } = string.Empty;

        public bool CategoryIsActive { get; set; }

        public string RegionName { get; set; } = string.Empty;

        public string CityName { get; set; } = string.Empty;
    }

    // Every foreign key is required, so the inner joins never drop a request.
    public static IQueryable<RequestRow> Rows(IApplicationDbContext db) =>
        from r in db.ServiceRequests.AsNoTracking()
        join s in db.Services.AsNoTracking() on r.ServiceId equals s.Id
        join c in db.ServiceCategories.AsNoTracking() on s.CategoryId equals c.Id
        join region in db.Regions.AsNoTracking() on r.RegionId equals region.Id
        join city in db.Cities.AsNoTracking() on r.CityId equals city.Id
        select new RequestRow
        {
            Request = r,
            ServiceName = s.Name,
            ServiceIsActive = s.IsActive,
            CategoryId = c.Id,
            CategoryName = c.Name,
            CategoryIsActive = c.IsActive,
            RegionName = region.Name,
            CityName = city.Name
        };

    public static RequestDto ToRequestDto(RequestRow x) => new(
        x.Request.Id, x.Request.ServiceId, x.ServiceName, x.CategoryId, x.CategoryName,
        x.Request.Title, x.Request.Description, x.Request.Address,
        x.Request.RegionId, x.RegionName, x.Request.CityId, x.CityName,
        x.Request.BudgetMin, x.Request.BudgetMax, x.Request.PreferredDate, x.Request.PreferredTime,
        x.Request.Status, x.Request.CancelledReason, x.Request.CreatedAt, x.Request.UpdatedAt);

    public static RequestListItemDto ToListItem(RequestRow x) => new(
        x.Request.Id, x.Request.Title, x.Request.ServiceId, x.ServiceName, x.CategoryName, x.CityName,
        x.Request.BudgetMin, x.Request.BudgetMax, x.Request.PreferredDate, x.Request.Status, x.Request.CreatedAt);

    public static FeedItemDto ToFeedItem(RequestRow x) => new(
        x.Request.Id, x.Request.Title, Summarize(x.Request.Description), x.Request.ServiceId, x.ServiceName,
        x.CategoryName, x.CityName, x.Request.BudgetMin, x.Request.BudgetMax, x.Request.PreferredDate,
        x.Request.PreferredTime, x.Request.Status, x.Request.CreatedAt);

    public static FeedDetailDto ToFeedDetail(RequestRow x) => new(
        x.Request.Id, x.Request.Title, x.Request.Description, x.Request.ServiceId, x.ServiceName, x.CategoryId,
        x.CategoryName, x.RegionName, x.CityName, x.Request.BudgetMin, x.Request.BudgetMax, x.Request.PreferredDate,
        x.Request.PreferredTime, x.Request.Status, x.Request.CreatedAt);

    private static string Summarize(string description)
    {
        var text = description.Trim();

        return text.Length <= SummaryLength ? text : text[..SummaryLength].TrimEnd() + "...";
    }
}