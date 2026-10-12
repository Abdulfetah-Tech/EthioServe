using EthioServe.Domain.Common;
using EthioServe.Domain.Enums;

namespace EthioServe.Domain.Entities;

public sealed class ServiceRequest : AuditableEntity
{
    private ServiceRequest() { }

    public ServiceRequest(
        Guid customerId, Guid serviceId, string title, string description, string address,
        int regionId, int cityId, decimal? budgetMin = null, decimal? budgetMax = null,
        DateOnly? preferredDate = null, TimeOnly? preferredTime = null)
    {
        if (budgetMin < 0 || budgetMax < 0)
        {
            throw new DomainException("Budget cannot be negative.");
        }

        if (budgetMax < budgetMin)
        {
            throw new DomainException("budgetMax cannot be less than budgetMin.");
        }

        CustomerId = customerId;
        ServiceId = serviceId;
        Title = Guard.NotBlank(title, nameof(title));
        Description = Guard.NotBlank(description, nameof(description));
        Address = Guard.NotBlank(address, nameof(address));
        RegionId = regionId;
        CityId = cityId;
        BudgetMin = budgetMin;
        BudgetMax = budgetMax;
        PreferredDate = preferredDate;
        PreferredTime = preferredTime;
        Status = ServiceRequestStatus.Pending;
    }

    public Guid CustomerId { get; private set; }

    public Guid ServiceId { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public decimal? BudgetMin { get; private set; }

    public decimal? BudgetMax { get; private set; }

    public DateOnly? PreferredDate { get; private set; }

    public TimeOnly? PreferredTime { get; private set; }

    public string Address { get; private set; } = string.Empty;

    public int RegionId { get; private set; }

    public int CityId { get; private set; }

    /// <summary>Changed only through the state machine (Phase 5+), never directly by callers.</summary>
    public ServiceRequestStatus Status { get; private set; }

    public string? CancelledReason { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    /// <summary>PostgreSQL xmin: optimistic concurrency token.</summary>
    public uint Version { get; private set; }
}