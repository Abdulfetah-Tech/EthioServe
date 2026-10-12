using EthioServe.Domain.Entities;
using EthioServe.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace EthioServe.Tests.Schema;

[Collection(DatabaseCollection.Name)]
public sealed class DeleteBehaviorTests(DatabaseFixture fixture)
{
    [Fact]
    public async Task Region_with_cities_cannot_be_deleted()
    {
        await using var db = fixture.CreateContext();
        var world = await TestData.CreateWorldAsync(db);

        var error = await DbAssert.RejectedAsync(() =>
            db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM regions WHERE id = {world.RegionId}"));

        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, error.SqlState);
    }

    [Fact]
    public async Task Service_used_by_a_request_cannot_be_deleted()
    {
        await using var db = fixture.CreateContext();
        var world = await TestData.CreateWorldAsync(db);

        var error = await DbAssert.RejectedAsync(() =>
            db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM services WHERE id = {world.ServiceId}"));

        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, error.SqlState);
    }

    [Fact]
    public async Task Provider_with_quotes_cannot_be_deleted()
    {
        await using var db = fixture.CreateContext();
        var world = await TestData.CreateWorldAsync(db);
        await TestData.AddQuoteAsync(db, world);

        var error = await DbAssert.RejectedAsync(() =>
            db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM provider_profiles WHERE id = {world.ProviderId}"));

        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, error.SqlState);
    }

    [Fact]
    public async Task Deleting_a_request_deletes_its_attachments()
    {
        await using var db = fixture.CreateContext();
        var world = await TestData.CreateWorldAsync(db);
        db.RequestAttachments.Add(new RequestAttachment(
            world.RequestId, $"key-{Guid.NewGuid():N}", "photo.jpg", "image/jpeg", 1024));
        await db.SaveChangesAsync();

        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM service_requests WHERE id = {world.RequestId}");

        var remaining = await db.RequestAttachments.CountAsync(a => a.ServiceRequestId == world.RequestId);
        Assert.Equal(0, remaining);
    }

    [Fact]
    public async Task Deleting_a_provider_deletes_its_service_offers()
    {
        await using var db = fixture.CreateContext();
        var world = await TestData.CreateWorldAsync(db);
        var providerId = await TestData.AddProviderAsync(db, world);
        db.ProviderServices.Add(new ProviderService(providerId, world.ServiceId, 300m));
        await db.SaveChangesAsync();

        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM provider_profiles WHERE id = {providerId}");

        var remaining = await db.ProviderServices.CountAsync(p => p.ProviderId == providerId);
        Assert.Equal(0, remaining);
    }
}