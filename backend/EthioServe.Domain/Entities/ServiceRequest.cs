using EthioServe.Domain.Common;
using EthioServe.Domain.Enums;

namespace EthioServe.Domain.Entities;

public sealed class ServiceRequest : AuditableEntity
{
    public const int MaxReasonLength = 500;

    private ServiceRequest() { }

    public ServiceRequest(
        Guid customerId, Guid serviceId, string title, string description, string address,
        int regionId, int cityId, decimal? budgetMin = null, decimal? budgetMax = null,
        DateOnly? preferredDate = null, TimeOnly? preferredTime = null)
    {
        ValidateBudget(budgetMin, budgetMax);

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

    /// <summary>Changed only through the methods below, never directly by callers.</summary>
    public ServiceRequestStatus Status { get; private set; }

    public string? CancelledReason { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    /// <summary>PostgreSQL xmin: optimistic concurrency token.</summary>
    public uint Version { get; private set; }

    /// <summary>The customer can change the details only until a provider has responded. The service itself is fixed.</summary>
    public void UpdateDetails(
        string title, string description, string address, int regionId, int cityId,
        decimal? budgetMin, decimal? budgetMax, DateOnly? preferredDate, TimeOnly? preferredTime)
    {
        if (Status != ServiceRequestStatus.Pending)
        {
            throw new DomainException($"A request that is {Status} can no longer be edited.");
        }

        ValidateBudget(budgetMin, budgetMax);

        Title = Guard.NotBlank(title, nameof(title));
        Description = Guard.NotBlank(description, nameof(description));
        Address = Guard.NotBlank(address, nameof(address));
        RegionId = regionId;
        CityId = cityId;
        BudgetMin = budgetMin;
        BudgetMax = budgetMax;
        PreferredDate = preferredDate;
        PreferredTime = preferredTime;
    }

    /// <summary>The first provider quote arrived (used by the quote workflow in the next phase).</summary>
    public void MarkQuoted()
    {
        if (Status != ServiceRequestStatus.Pending)
        {
            throw new DomainException($"A request that is {Status} cannot receive its first quote.");
        }

        Status = ServiceRequestStatus.Quoted;
    }

    public void Cancel(string? reason)
    {
        if (Status is not (ServiceRequestStatus.Pending or ServiceRequestStatus.Quoted))
        {
            throw new DomainException($"A request that is {Status} cannot be cancelled.");
        }

        var note = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();

        if (note is { Length: > MaxReasonLength })
        {
            throw new DomainException($"The reason cannot be longer than {MaxReasonLength} characters.");
        }

        Status = ServiceRequestStatus.Cancelled;
        CancelledReason = note;
    }

    private static void ValidateBudget(decimal? min, decimal? max)
    {
        if (min < 0 || max < 0)
        {
            throw new DomainException("Budget cannot be negative.");
        }

        if (max < min)
        {
            throw new DomainException("budgetMax cannot be less than budgetMin.");
        }
    }
}