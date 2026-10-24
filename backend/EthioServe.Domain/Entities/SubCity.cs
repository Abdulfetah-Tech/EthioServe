using EthioServe.Domain.Common;

namespace EthioServe.Domain.Entities;

public sealed class SubCity
{
    private SubCity() { }

    public SubCity(int cityId, string name, string? nameAm = null)
    {
        CityId = cityId;
        Name = Guard.NotBlank(name, nameof(name));
        NameAm = nameAm;
    }

    public int Id { get; private set; }

    public int CityId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string? NameAm { get; private set; }
}