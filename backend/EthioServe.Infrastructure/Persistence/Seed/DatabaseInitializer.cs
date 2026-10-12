using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EthioServe.Infrastructure.Persistence.Seed;

public static class DatabaseInitializer
{
    /// <summary>Applies pending migrations, then seeds reference data. Intended for Development only.</summary>
    public static async Task InitializeDatabaseAsync(this IServiceProvider services, CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await db.Database.MigrateAsync(ct);
        await DatabaseSeeder.SeedAsync(db, ct);
    }
}