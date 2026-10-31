using EthioServe.Domain.Common;

namespace EthioServe.Domain.Entities;

public sealed class Review : AuditableEntity
{
    public const int MaxCommentLength = 2000;

    private Review() { }

    public Review(Guid serviceRequestId, Guid customerId, Guid providerId, int rating, string? comment = null)
    {
        ServiceRequestId = serviceRequestId;
        CustomerId = customerId;
        ProviderId = providerId;
        Rating = Guard.InRange(rating, 1, 5, nameof(rating));
        Comment = CleanComment(comment);
        IsVisible = true;
    }

    public Guid ServiceRequestId { get; private set; }

    public Guid CustomerId { get; private set; }

    public Guid ProviderId { get; private set; }

    public int Rating { get; private set; }

    public string? Comment { get; private set; }

    /// <summary>False when an admin hides the review during moderation.</summary>
    public bool IsVisible { get; private set; }

    public void Hide() => IsVisible = false;

    public void Show() => IsVisible = true;

    private static string? CleanComment(string? comment)
    {
        var text = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();

        if (text is { Length: > MaxCommentLength })
        {
            throw new DomainException($"The comment cannot be longer than {MaxCommentLength} characters.");
        }

        return text;
    }
}
