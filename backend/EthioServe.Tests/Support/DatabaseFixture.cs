using EthioServe.Application.Abstractions;
using EthioServe.Infrastructure.Persistence;
using EthioServe.Infrastructure.Time;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace EthioServe.Tests.Support;

/// <summary>
/// One throwaway PostgreSQL container with the REAL migrations applied, shared by all schema tests.
/// It never touches your development database.
/// </summary>
public sealed class DatabaseFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17").Build();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        await using var db = CreateContext();
        await db.Database.MigrateAsync();
    }

    public AppDbContext CreateContext(IClock? clock = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .UseSnakeCaseNamingConvention()
            .Options;

        return new AppDbContext(options, clock ?? new SystemClock());
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();
}