using EthioServe.API.Configuration;
using EthioServe.Infrastructure.Persistence;
using EthioServe.Infrastructure.Persistence.Seed;
using EthioServe.Infrastructure.Time;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using EthioServe.Application.Abstractions;
namespace EthioServe.Tests.Support;

public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17").Build();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        var connectionString = _postgres.GetConnectionString();

        // Environment variables are loaded before Program.cs runs and win over user-secrets,
        // so these tests can never touch your development database.
        Environment.SetEnvironmentVariable("ConnectionStrings__Default", connectionString);
        Environment.SetEnvironmentVariable("Jwt__SigningKey", "integration-test-signing-key-0123456789-abcdef");

        // The "Testing" environment does not migrate or seed on startup, so do it here.
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention()
            .Options;

        await using var db = new AppDbContext(options, new SystemClock());
        await db.Database.MigrateAsync();
        await DatabaseSeeder.SeedAsync(db);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureTestServices(services =>
        {
            services.AddControllers().AddApplicationPart(typeof(ErrorsTestController).Assembly);
            services.AddSingleton<TestEmailService>();
            services.AddSingleton<IEmailService>(sp => sp.GetRequiredService<TestEmailService>());
            // High limits for the normal run; the rate-limit test lowers them on its own server.
            services.Configure<RateLimitSettings>(settings =>
            {
                settings.GlobalPermitsPerMinute = 100_000;
                settings.AuthPermitsPerMinute = 100_000;
            });
        });
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}