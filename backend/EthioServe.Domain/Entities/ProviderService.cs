using EthioServe.Domain.Common;

namespace EthioServe.Domain.Entities;

/// <summary>A service a provider offers, with the provider's own price.</summary>
public sealed class ProviderService : BaseEntity
{
    private ProviderService() { }

    public ProviderService(Guid providerId, Guid serviceId, decimal price, string? description = null)
    {
        ProviderId = providerId;
        ServiceId = serviceId;
        Price = Guard.NotNegative(price, nameof(price));
        Description = description;
        IsActive = true;
    }

    public Guid ProviderId { get; private set; }

    public Guid ServiceId { get; private set; }

    public decimal Price { get; private set; }

    public string? Description { get; private set; }

    public bool IsActive { get; private set; }
}