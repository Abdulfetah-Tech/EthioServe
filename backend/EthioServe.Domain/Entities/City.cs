using EthioServe.Domain.Common;

namespace EthioServe.Domain.Entities;

public sealed class City
{
    private City() { }

    public City(int regionId, string name, string? nameAm = null)
    {
        RegionId = regionId;
        Name = Guard.NotBlank(name, nameof(name));
        NameAm = nameAm;
    }

    public int Id { get; private set; }

    public int RegionId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string? NameAm { get; private set; }
}