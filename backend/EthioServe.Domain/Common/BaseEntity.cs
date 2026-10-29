using System;

namespace EthioServe.Domain.Common;

/// <summary>Guid-keyed entity with a creation timestamp (stamped by the DbContext, never by callers).</summary>
public abstract class BaseEntity : ICreationAudited
{
    // Version 7 GUIDs are time-ordered, which keeps B-tree indexes compact and inserts fast.
    public Guid Id { get; protected set; } = Guid.CreateVersion7();

    public DateTimeOffset CreatedAt { get; private set; }

    public virtual void MarkCreated(DateTimeOffset utcNow)
    {
        if (CreatedAt == default)
        {
            // Truncate to milliseconds to avoid precision differences between DB and SignalR payloads
            var ticks = utcNow.Ticks - (utcNow.Ticks % TimeSpan.TicksPerMillisecond);
            CreatedAt = new DateTimeOffset(ticks, utcNow.Offset);
        }
    }
}