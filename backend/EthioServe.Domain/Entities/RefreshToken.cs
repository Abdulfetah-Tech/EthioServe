using EthioServe.Domain.Common;

namespace EthioServe.Domain.Entities;

/// <summary>
/// A refresh token as stored in the database. Only a SHA-256 hash of the token is kept, never the token itself.
/// All tokens issued from one login (and its rotations) share a FamilyId, so replaying an old token
/// can revoke the whole family with a single update.
/// </summary>
public sealed class RefreshToken : BaseEntity
{
    private RefreshToken() { }

    public RefreshToken(Guid userId, Guid familyId, string tokenHash, DateTimeOffset expiresAt, string createdByIp)
    {
        UserId = userId;
        FamilyId = familyId;
        TokenHash = Guard.NotBlank(tokenHash, nameof(tokenHash));
        ExpiresAt = expiresAt;
        CreatedByIp = createdByIp;
    }

    public Guid UserId { get; private set; }

    public Guid FamilyId { get; private set; }

    public string TokenHash { get; private set; } = string.Empty;

    public DateTimeOffset ExpiresAt { get; private set; }

    public string CreatedByIp { get; private set; } = string.Empty;

    public DateTimeOffset? RevokedAt { get; private set; }

    /// <summary>Hash of the token that replaced this one when it was rotated.</summary>
    public string? ReplacedByTokenHash { get; private set; }

    public bool IsRevoked => RevokedAt is not null;

    public bool IsExpired(DateTimeOffset now) => now >= ExpiresAt;

    public bool IsActive(DateTimeOffset now) => !IsRevoked && !IsExpired(now);

    public void Revoke(DateTimeOffset now, string? replacedByTokenHash = null)
    {
        if (IsRevoked)
        {
            return; // keep the first revocation: it is the audit trail
        }

        RevokedAt = now;
        ReplacedByTokenHash = replacedByTokenHash;
    }
}