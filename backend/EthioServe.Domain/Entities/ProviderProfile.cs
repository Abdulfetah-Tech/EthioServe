using EthioServe.Domain.Common;
using EthioServe.Domain.Enums;

namespace EthioServe.Domain.Entities;

public sealed class ProviderProfile : AuditableEntity
{
    private ProviderProfile() { }

    public ProviderProfile(
        Guid userId, string businessName, string phoneNumber, int regionId, int cityId,
        int? subCityId = null, string description = "", string address = "", int yearsOfExperience = 0)
    {
        if (yearsOfExperience < 0)
        {
            throw new DomainException("yearsOfExperience cannot be negative.");
        }

        UserId = userId;
        BusinessName = Guard.NotBlank(businessName, nameof(businessName));
        PhoneNumber = Guard.NotBlank(phoneNumber, nameof(phoneNumber));
        RegionId = regionId;
        CityId = cityId;
        SubCityId = subCityId;
        Description = description;
        Address = address;
        YearsOfExperience = yearsOfExperience;
        VerificationStatus = VerificationStatus.Unverified;
        IsAvailable = true;
    }

    public Guid UserId { get; private set; }

    public string BusinessName { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public string PhoneNumber { get; private set; } = string.Empty;

    public string? ProfileImageKey { get; private set; }

    public int YearsOfExperience { get; private set; }

    public VerificationStatus VerificationStatus { get; private set; }

    /// <summary>Denormalized aggregate, recalculated in the same transaction as every review change (Phase 9).</summary>
    public decimal AverageRating { get; private set; }

    public int TotalReviews { get; private set; }

    public int RegionId { get; private set; }

    public int CityId { get; private set; }

    public int? SubCityId { get; private set; }

    public string Address { get; private set; } = string.Empty;

    public decimal? Latitude { get; private set; }

    public decimal? Longitude { get; private set; }

    public bool IsAvailable { get; private set; }
}