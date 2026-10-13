using EthioServe.Domain.Common;
using EthioServe.Domain.Entities;

namespace EthioServe.Tests.Unit;

public sealed class RefreshTokenTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 12, 8, 0, 0, TimeSpan.Zero);

    private static RefreshToken NewToken(DateTimeOffset? expiresAt = null) =>
        new(Guid.NewGuid(), Guid.NewGuid(), "hash-1", expiresAt ?? Now.AddDays(7), "127.0.0.1");

    [Fact]
    public void New_token_is_active()
    {
        Assert.True(NewToken().IsActive(Now));
    }

    [Fact]
    public void Token_is_inactive_once_expired()
    {
        var token = NewToken(Now.AddMinutes(1));

        Assert.True(token.IsActive(Now));
        Assert.False(token.IsActive(Now.AddMinutes(1)));
    }

    [Fact]
    public void Revoked_token_is_inactive_and_remembers_its_replacement()
    {
        var token = NewToken();

        token.Revoke(Now, "hash-2");

        Assert.True(token.IsRevoked);
        Assert.False(token.IsActive(Now));
        Assert.Equal("hash-2", token.ReplacedByTokenHash);
    }

    [Fact]
    public void Revoking_twice_keeps_the_first_revocation()
    {
        var token = NewToken();

        token.Revoke(Now, "hash-2");
        token.Revoke(Now.AddHours(1), "hash-3");

        Assert.Equal(Now, token.RevokedAt);
        Assert.Equal("hash-2", token.ReplacedByTokenHash);
    }

    [Fact]
    public void Blank_token_hash_is_rejected()
    {
        Assert.Throws<DomainException>(() =>
            new RefreshToken(Guid.NewGuid(), Guid.NewGuid(), " ", Now.AddDays(1), "127.0.0.1"));
    }
}