using EthioServe.Domain.Common;

namespace EthioServe.Domain.Entities;

public sealed class Review : AuditableEntity
{
    private Review() { }

    public Review(Guid serviceRequestId, Guid customerId, Guid providerId, int rating, string? comment = null)
    {
        ServiceRequestId = serviceRequestId;
        CustomerId = customerId;
        ProviderId = providerId;
        Rating = Guard.InRange(rating, 1, 5, nameof(rating));
        Comment = comment;
        IsVisible = true;
    }

    public Guid ServiceRequestId { get; private set; }

    public Guid CustomerId { get; private set; }

    public Guid ProviderId { get; private set; }

    public int Rating { get; private set; }

    public string? Comment { get; private set; }

    /// <summary>False when an admin hides the review during moderation.</summary>
    public bool IsVisible { get; private set; }
}