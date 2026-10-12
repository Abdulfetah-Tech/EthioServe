namespace EthioServe.Domain.Common;

/// <summary>Entity that also tracks its last modification time.</summary>
public abstract class AuditableEntity : BaseEntity, IModificationAudited
{
    public DateTimeOffset UpdatedAt { get; private set; }

    public override void MarkCreated(DateTimeOffset utcNow)
    {
        base.MarkCreated(utcNow);

        if (UpdatedAt == default)
        {
            UpdatedAt = CreatedAt;
        }
    }

    public void MarkUpdated(DateTimeOffset utcNow) => UpdatedAt = utcNow;
}