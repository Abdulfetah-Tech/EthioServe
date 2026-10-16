using EthioServe.Domain.Common;

namespace EthioServe.Domain.Entities;

public sealed class ServiceCategory : AuditableEntity
{
    private ServiceCategory() { }

    public ServiceCategory(string name, string? description = null, string? iconKey = null)
    {
        Name = Guard.NotBlank(name, nameof(name));
        Description = description;
        IconKey = iconKey;
        IsActive = true;
    }

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public string? IconKey { get; private set; }

    public bool IsActive { get; private set; }

    public void Update(string name, string? description, string? iconKey)
    {
        Name = Guard.NotBlank(name, nameof(name));
        Description = description;
        IconKey = iconKey;
    }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;
}