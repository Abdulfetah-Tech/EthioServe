using EthioServe.Domain.Entities;
using EthioServe.Infrastructure.Persistence;
using EthioServe.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace EthioServe.Tests.Schema;

[Collection(DatabaseCollection.Name)]
public sealed class RefreshTokenSchemaTests(DatabaseFixture fixture)
{
    private static async Task<Guid> CreateUserAsync(AppDbContext db)
    {
        var tag = Guid.NewGuid().ToString("N")[..10];
        var user = TestData.NewUser($"user-{tag}", $"user-{tag}@test.local");
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    private static RefreshToken NewToken(Guid userId, string hash) =>
        new(userId, Guid.NewGuid(), hash, DateTimeOffset.UtcNow.AddDays(7), "127.0.0.1");

    [Fact]
    public async Task Token_hash_must_be_unique()
    {
        await using var db = fixture.CreateContext();
        var userId = await CreateUserAsync(db);
        var hash = $"hash-{Guid.NewGuid():N}";
        db.RefreshTokens.Add(NewToken(userId, hash));
        await db.SaveChangesAsync();

        await using var other = fixture.CreateContext();
        other.RefreshTokens.Add(NewToken(userId, hash));
        var error = await DbAssert.RejectedAsync(() => other.SaveChangesAsync());

        DbAssert.Violates(error, PostgresErrorCodes.UniqueViolation, "ux_refresh_tokens_token_hash");
    }

    [Fact]
    public async Task Deleting_a_user_deletes_their_refresh_tokens()
    {
        await using var db = fixture.CreateContext();
        var userId = await CreateUserAsync(db);
        db.RefreshTokens.Add(NewToken(userId, $"hash-{Guid.NewGuid():N}"));
        await db.SaveChangesAsync();

        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM users WHERE id = {userId}");

        var remaining = await db.RefreshTokens.CountAsync(t => t.UserId == userId);
        Assert.Equal(0, remaining);
    }

    [Fact]
    public async Task Revocation_is_saved_and_loaded_back()
    {
        await using var db = fixture.CreateContext();
        var userId = await CreateUserAsync(db);
        var token = NewToken(userId, $"hash-{Guid.NewGuid():N}");
        db.RefreshTokens.Add(token);
        await db.SaveChangesAsync();

        var now = new DateTimeOffset(2026, 10, 12, 8, 0, 0, TimeSpan.Zero);
        token.Revoke(now, "next-hash");
        await db.SaveChangesAsync();

        await using var reader = fixture.CreateContext();
        var loaded = await reader.RefreshTokens.SingleAsync(t => t.Id == token.Id);
        Assert.True(loaded.IsRevoked);
        Assert.Equal(now, loaded.RevokedAt);
        Assert.Equal("next-hash", loaded.ReplacedByTokenHash);
    }
}