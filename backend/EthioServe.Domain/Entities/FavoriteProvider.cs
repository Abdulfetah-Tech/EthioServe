using EthioServe.Domain.Common;

namespace EthioServe.Domain.Entities;

/// <summary>A customer bookmark for a public provider. Neither side's contact details are stored here.</summary>
public sealed class FavoriteProvider : BaseEntity
{
    private FavoriteProvider() { }

    public FavoriteProvider(Guid customerId, Guid providerId)
    {
        CustomerId = customerId;
        ProviderId = providerId;
    }

    public Guid CustomerId { get; private set; }

    public Guid ProviderId { get; private set; }
}
