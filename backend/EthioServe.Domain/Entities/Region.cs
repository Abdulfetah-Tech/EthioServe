using EthioServe.Domain.Common;

namespace EthioServe.Domain.Entities;

public sealed class Region
{
    private Region() { }

    public Region(string name, string? nameAm = null)
    {
        Name = Guard.NotBlank(name, nameof(name));
        NameAm = nameAm;
    }

    public int Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string? NameAm { get; private set; }
}