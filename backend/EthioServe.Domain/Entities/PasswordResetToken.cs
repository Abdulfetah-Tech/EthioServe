using EthioServe.Domain.Common;

namespace EthioServe.Domain.Entities;

/// <summary>A single-use password reset link. Only the SHA-256 hash of the token is stored.</summary>
public sealed class PasswordResetToken : BaseEntity
{
    private PasswordResetToken() { }

    public PasswordResetToken(Guid userId, string tokenHash, DateTimeOffset expiresAt, string requestedFromIp)
    {
        UserId = userId;
        TokenHash = Guard.NotBlank(tokenHash, nameof(tokenHash));
        ExpiresAt = expiresAt;
        RequestedFromIp = requestedFromIp;
    }

    public Guid UserId { get; private set; }

    public string TokenHash { get; private set; } = string.Empty;

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? UsedAt { get; private set; }

    public string RequestedFromIp { get; private set; } = string.Empty;

    public bool IsUsable(DateTimeOffset now) => UsedAt is null && now < ExpiresAt;

    public void MarkUsed(DateTimeOffset now) => UsedAt ??= now;
}