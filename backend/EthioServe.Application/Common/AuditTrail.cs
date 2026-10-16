using System.Text.Json;
using System.Text.Json.Serialization;
using EthioServe.Application.Abstractions;
using EthioServe.Domain.Entities;

namespace EthioServe.Application.Common;

public interface IAuditTrail
{
    /// <summary>
    /// Queues an audit entry. It is saved by the caller's SaveChanges, so the change and its audit record
    /// succeed or fail together. An entry whose old and new values are identical (a no-op) is not written.
    /// </summary>
    void Record(string action, string entityName, Guid entityId, object? oldValues, object? newValues);
}

public sealed class AuditTrail(IApplicationDbContext db, ICurrentUser currentUser) : IAuditTrail
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public void Record(string action, string entityName, Guid entityId, object? oldValues, object? newValues)
    {
        var oldJson = oldValues is null ? null : JsonSerializer.Serialize(oldValues, Json);
        var newJson = newValues is null ? null : JsonSerializer.Serialize(newValues, Json);

        if (oldJson is not null && oldJson == newJson)
        {
            return;
        }

        db.AuditLogs.Add(new AuditLog(
            currentUser.UserId,
            action,
            entityName,
            entityId.ToString(),
            oldJson,
            newJson,
            Truncate(currentUser.IpAddress, 45),
            Truncate(currentUser.UserAgent, 300)));
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length > maxLength ? value[..maxLength] : value;
}