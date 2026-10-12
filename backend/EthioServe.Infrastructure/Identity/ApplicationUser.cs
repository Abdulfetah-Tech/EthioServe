using EthioServe.Domain.Common;
using Microsoft.AspNetCore.Identity;

namespace EthioServe.Infrastructure.Identity;

/// <summary>
/// The login account. Lives in Infrastructure so the Domain stays free of framework dependencies;
/// domain entities reference it only by Guid (UserId).
/// </summary>
public sealed class ApplicationUser : IdentityUser<Guid>, ICreationAudited
{
    public bool IsSuspended { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? LastLoginAt { get; private set; }

    public void MarkCreated(DateTimeOffset utcNow)
    {
        if (CreatedAt == default)
        {
            CreatedAt = utcNow;
        }
    }

    public void Suspend() => IsSuspended = true;

    public void Reinstate() => IsSuspended = false;

    public void RecordLogin(DateTimeOffset utcNow) => LastLoginAt = utcNow;
}