using EthioServe.Domain.Common;
using EthioServe.Domain.Enums;

namespace EthioServe.Domain.Entities;

public sealed class Service : AuditableEntity
{
    private Service() { }

    public Service(
        Guid categoryId, string name, PricingType pricingType,
        decimal? basePrice = null, string? description = null)
    {
        if (basePrice is not null)
        {
            Guard.NotNegative(basePrice.Value, nameof(basePrice));
        }

        CategoryId = categoryId;
        Name = Guard.NotBlank(name, nameof(name));
        PricingType = pricingType;
        BasePrice = basePrice;
        Description = description;
        IsActive = true;
    }

    public Guid CategoryId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public decimal? BasePrice { get; private set; }

    public PricingType PricingType { get; private set; }

    public bool IsActive { get; private set; }

    /// <summary>The category is fixed at creation; everything else can be edited.</summary>
    public void Update(string name, string? description, decimal? basePrice, PricingType pricingType)
    {
        if (basePrice is not null)
        {
            Guard.NotNegative(basePrice.Value, nameof(basePrice));
        }

        Name = Guard.NotBlank(name, nameof(name));
        Description = description;
        BasePrice = basePrice;
        PricingType = pricingType;
    }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;
}