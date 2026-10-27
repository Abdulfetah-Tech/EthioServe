using EthioServe.Application.Abstractions;
using EthioServe.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EthioServe.Application.Appointments;

/// <summary>One query shape for both sides. Each service filters to its own user and maps only what that side may see.</summary>
internal static class AppointmentProjections
{
    internal sealed class Row
    {
        public Guid Id { get; set; }

        public Guid RequestId { get; set; }

        public string RequestTitle { get; set; } = string.Empty;

        public string ServiceName { get; set; } = string.Empty;

        public string CityName { get; set; } = string.Empty;

        public DateTimeOffset StartAt { get; set; }

        public DateTimeOffset EndAt { get; set; }

        public string Address { get; set; } = string.Empty;

        public string? Notes { get; set; }

        public AppointmentStatus Status { get; set; }

        public Guid CustomerId { get; set; }

        public string CustomerName { get; set; } = string.Empty;

        public string CustomerPhone { get; set; } = string.Empty;

        public Guid ProviderId { get; set; }

        public string ProviderName { get; set; } = string.Empty;

        public string ProviderPhone { get; set; } = string.Empty;

        public decimal Amount { get; set; }

        public DateTimeOffset CreatedAt { get; set; }
    }

    public static IQueryable<Row> Rows(IApplicationDbContext db) =>
        from a in db.Appointments.AsNoTracking()
        join r in db.ServiceRequests.AsNoTracking() on a.ServiceRequestId equals r.Id
        join s in db.Services.AsNoTracking() on r.ServiceId equals s.Id
        join c in db.Cities.AsNoTracking() on r.CityId equals c.Id
        join cp in db.CustomerProfiles.AsNoTracking() on a.CustomerId equals cp.Id
        join pp in db.ProviderProfiles.AsNoTracking() on a.ProviderId equals pp.Id
        join q in db.Quotes.AsNoTracking() on a.QuoteId equals q.Id
        select new Row
        {
            Id = a.Id,
            RequestId = r.Id,
            RequestTitle = r.Title,
            ServiceName = s.Name,
            CityName = c.Name,
            StartAt = a.StartAt,
            EndAt = a.EndAt,
            Address = a.Address,
            Notes = a.Notes,
            Status = a.Status,
            CustomerId = a.CustomerId,
            CustomerName = cp.FirstName + " " + cp.LastName,
            CustomerPhone = cp.PhoneNumber,
            ProviderId = a.ProviderId,
            ProviderName = pp.BusinessName,
            ProviderPhone = pp.PhoneNumber,
            Amount = q.Amount,
            CreatedAt = a.CreatedAt
        };

    public static IQueryable<Row> InScope(IQueryable<Row> rows, AppointmentScope scope) =>
        scope == AppointmentScope.Upcoming
            ? rows.Where(x => x.Status == AppointmentStatus.Scheduled
                              || x.Status == AppointmentStatus.Confirmed
                              || x.Status == AppointmentStatus.InProgress)
            : rows.Where(x => x.Status == AppointmentStatus.Completed
                              || x.Status == AppointmentStatus.Cancelled
                              || x.Status == AppointmentStatus.NoShow);

    public static IOrderedQueryable<Row> Ordered(IQueryable<Row> rows, AppointmentScope scope) =>
        scope == AppointmentScope.Upcoming
            ? rows.OrderBy(x => x.StartAt).ThenBy(x => x.Id)
            : rows.OrderByDescending(x => x.StartAt).ThenByDescending(x => x.Id);
}