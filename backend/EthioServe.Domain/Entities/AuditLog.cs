using EthioServe.Domain.Common;

namespace EthioServe.Domain.Entities;

/// <summary>Append-only record of a sensitive action. There are intentionally no mutating methods.</summary>
public sealed class AuditLog : ICreationAudited
{
    private AuditLog() { }

    public AuditLog(
        Guid? userId, string action, string entityName, string entityId,
        string? oldValues, string? newValues, string ipAddress, string userAgent)
    {
        UserId = userId;
        Action = Guard.NotBlank(action, nameof(action));
        EntityName = Guard.NotBlank(entityName, nameof(entityName));
        EntityId = Guard.NotBlank(entityId, nameof(entityId));
        OldValues = oldValues;
        NewValues = newValues;
        IpAddress = ipAddress;
        UserAgent = userAgent;
    }

    public long Id { get; private set; }

    /// <summary>Deliberately not a foreign key, so history survives user deletion.</summary>
    public Guid? UserId { get; private set; }

    public string Action { get; private set; } = string.Empty;

    public string EntityName { get; private set; } = string.Empty;

    public string EntityId { get; private set; } = string.Empty;

    public string? OldValues { get; private set; }

    public string? NewValues { get; private set; }

    public string IpAddress { get; private set; } = string.Empty;

    public string UserAgent { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public void MarkCreated(DateTimeOffset utcNow)
    {
        if (CreatedAt == default)
        {
            CreatedAt = utcNow;
        }
    }
}