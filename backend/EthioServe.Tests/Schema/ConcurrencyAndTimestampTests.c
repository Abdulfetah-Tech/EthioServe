using EthioServe.Domain.Entities;
using EthioServe.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace EthioServe.Tests.Schema;

[Collection(DatabaseCollection.Name)]
public sealed class ConcurrencyAndTimestampTests(DatabaseFixture fixture)
{
    [Fact]
    public async Task Row_version_is_read_from_the_xmin_system_column()
    {
        await using var db = fixture.CreateContext();
        var world = await TestData.CreateWorldAsync(db);

        await using var reader = fixture.CreateContext();
        var request = await reader.ServiceRequests.SingleAsync(r => r.Id == world.RequestId);

        Assert.NotEqual(0u, request.Version);
    }

    [Fact]
    public async Task Saving_an_edit_changes_the_row_version()
    {
        await using var db = fixture.CreateContext();
        var world = await TestData.CreateWorldAsync(db);
        var quote = await TestData.AddQuoteAsync(db, world);
        var before = quote.Version;

        db.Entry(quote).Property(q => q.Description).CurrentValue = "Revised description";
        await db.SaveChangesAsync();

        Assert.NotEqual(before, quote.Version);
    }

    [Fact]
    public async Task Concurrent_edits_of_the_same_quote_are_detected()
    {
        await using var setup = fixture.CreateContext();
        var world = await TestData.CreateWorldAsync(setup);
        var created = await TestData.AddQuoteAsync(setup, world);

        // Two fleet users open the same quote at the same time.
        await using var userA = fixture.CreateContext();
        await using var userB = fixture.CreateContext();
        var quoteA = await userA.Quotes.SingleAsync(q => q.Id == created.Id);
        var quoteB = await userB.Quotes.SingleAsync(q => q.Id == created.Id);

        userA.Entry(quoteA).Property(q => q.Description).CurrentValue = "Edited by A";
        await userA.SaveChangesAsync();

        userB.Entry(quoteB).Property(q => q.Description).CurrentValue = "Edited by B";

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => userB.SaveChangesAsync());
    }

    [Fact]
    public async Task SaveChanges_stamps_created_and_updated_times_from_the_clock()
    {
        var start = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);
        var clock = new TestClock(start);
        await using var db = fixture.CreateContext(clock);

        var category = new ServiceCategory($"Stamped-{Guid.NewGuid():N}");
        db.ServiceCategories.Add(category);
        await db.SaveChangesAsync();

        Assert.Equal(start, category.CreatedAt);
        Assert.Equal(start, category.UpdatedAt);

        clock.UtcNow = start.AddHours(5);
        db.Entry(category).Property(c => c.Description).CurrentValue = "changed";
        await db.SaveChangesAsync();

        await using var reader = fixture.CreateContext();
        var loaded = await reader.ServiceCategories.SingleAsync(c => c.Id == category.Id);
        Assert.Equal(start, loaded.CreatedAt);
        Assert.Equal(start.AddHours(5), loaded.UpdatedAt);
    }
}