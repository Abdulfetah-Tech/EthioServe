using EthioServe.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace EthioServe.Tests.Schema;

[Collection(DatabaseCollection.Name)]
public sealed class MigrationTests(DatabaseFixture fixture)
{
    private static readonly string[] ExpectedTables =
    [
        "users", "roles", "user_roles", "user_claims", "user_logins", "user_tokens", "role_claims",
        "regions", "cities", "sub_cities", "customer_profiles", "provider_profiles",
        "service_categories", "services", "provider_services", "provider_documents",
        "service_requests", "request_attachments", "quotes", "appointments", "payments",
        "reviews", "notifications", "audit_logs", "__EFMigrationsHistory"
    ];

    [Fact]
    public async Task Migrations_create_every_expected_table()
    {
        await using var db = fixture.CreateContext();

        var tables = await db.Database
            .SqlQueryRaw<string>("SELECT table_name::text AS \"Value\" FROM information_schema.tables WHERE table_schema = 'public'")
            .ToListAsync();

        foreach (var expected in ExpectedTables)
        {
            Assert.Contains(expected, tables);
        }
    }

    [Fact]
    public async Task No_migrations_are_pending()
    {
        await using var db = fixture.CreateContext();

        var pending = await db.Database.GetPendingMigrationsAsync();

        Assert.Empty(pending);
    }

    [Fact]
    public async Task Model_matches_the_latest_migration()
    {
        await using var db = fixture.CreateContext();

        // Fails when someone changes an entity or configuration but forgets to add a migration.
        Assert.False(db.Database.HasPendingModelChanges(), "The EF model has changes that no migration contains.");
    }

    [Fact]
    public async Task Enums_are_stored_as_readable_text()
    {
        await using var db = fixture.CreateContext();
        var world = await TestData.CreateWorldAsync(db);

        var status = await db.Database
            .SqlQuery<string>($"SELECT status::text AS \"Value\" FROM service_requests WHERE id = {world.RequestId}")
            .SingleAsync();

        Assert.Equal("Pending", status);
    }
}