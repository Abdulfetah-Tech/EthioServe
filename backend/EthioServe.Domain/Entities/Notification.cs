using EthioServe.Domain.Common;
using EthioServe.Domain.Enums;

namespace EthioServe.Domain.Entities;

public sealed class Notification : BaseEntity
{
    private Notification() { }

    public Notification(
        Guid userId, NotificationType type, string title, string message,
        string? relatedEntityType = null, Guid? relatedEntityId = null)
    {
        UserId = userId;
        Type = type;
        Title = Guard.NotBlank(title, nameof(title));
        Message = Guard.NotBlank(message, nameof(message));
        RelatedEntityType = relatedEntityType;
        RelatedEntityId = relatedEntityId;
        IsRead = false;
    }

    public Guid UserId { get; private set; }

    public NotificationType Type { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string Message { get; private set; } = string.Empty;

    public bool IsRead { get; private set; }

    public string? RelatedEntityType { get; private set; }

    public Guid? RelatedEntityId { get; private set; }
}