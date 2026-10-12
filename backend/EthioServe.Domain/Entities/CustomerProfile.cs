using EthioServe.Domain.Common;

namespace EthioServe.Domain.Entities;

public sealed class CustomerProfile : AuditableEntity
{
    private CustomerProfile() { }

    public CustomerProfile(
        Guid userId, string firstName, string lastName, string phoneNumber,
        int? regionId = null, int? cityId = null, int? subCityId = null, string? address = null)
    {
        UserId = userId;
        FirstName = Guard.NotBlank(firstName, nameof(firstName));
        LastName = Guard.NotBlank(lastName, nameof(lastName));
        PhoneNumber = Guard.NotBlank(phoneNumber, nameof(phoneNumber));
        RegionId = regionId;
        CityId = cityId;
        SubCityId = subCityId;
        Address = address;
    }

    /// <summary>Identity user id. The user itself lives in Infrastructure, so Domain references it by id only.</summary>
    public Guid UserId { get; private set; }

    public string FirstName { get; private set; } = string.Empty;

    public string LastName { get; private set; } = string.Empty;

    public string PhoneNumber { get; private set; } = string.Empty;

    public string? ProfileImageKey { get; private set; }

    public int? RegionId { get; private set; }

    public int? CityId { get; private set; }

    public int? SubCityId { get; private set; }

    public string? Address { get; private set; }
}