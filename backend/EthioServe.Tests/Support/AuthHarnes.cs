using EthioServe.Application;
using EthioServe.Application.Abstractions;
using EthioServe.Application.Auth;
using EthioServe.Infrastructure;
using EthioServe.Infrastructure.Identity;
using EthioServe.Infrastructure.Persistence;
using EthioServe.Infrastructure.Persistence.Seed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace EthioServe.Tests.Support;

public sealed class AuthHarness : IAsyncDisposable
{
    public const string StrongPassword = "Str0ngPassw0rd!";

    public static readonly ClientInfo Client = new("127.0.0.1", "xunit");

    private readonly ServiceProvider _provider;

    private AuthHarness(ServiceProvider provider, TestClock clock, JwtOptions jwt)
    {
        _provider = provider;
        Clock = clock;
        Jwt = jwt;
    }

    public TestClock Clock { get; }

    public JwtOptions Jwt { get; }

    public static async Task<AuthHarness> CreateAsync(DatabaseFixture fixture)
    {
        var settings = new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] = fixture.ConnectionString,
            ["Jwt:Issuer"] = "EthioServe",
            ["Jwt:Audience"] = "EthioServe.Web",
            ["Jwt:SigningKey"] = "harness-signing-key-0123456789-abcdefghijklmnop",
            ["Jwt:AccessTokenMinutes"] = "15",
            ["Jwt:RefreshTokenDays"] = "7"
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        var clock = new TestClock(DateTimeOffset.UtcNow);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        services.AddInfrastructure(configuration);
        services.AddSingleton<IClock>(clock); // last registration wins: tests now control time

        var provider = services.BuildServiceProvider(validateScopes: true);

        // Roles are reference data that registration depends on.
        await using (var scope = provider.CreateAsyncScope())
        {
            await DatabaseSeeder.SeedAsync(scope.ServiceProvider.GetRequiredService<AppDbContext>());
        }

        return new AuthHarness(provider, clock, provider.GetRequiredService<IOptions<JwtOptions>>().Value);
    }

    public async Task<T> Call<T>(Func<IAuthService, Task<T>> action)
    {
        await using var scope = _provider.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<IAuthService>());
    }

    public async Task CallVoid(Func<IAuthService, Task> action)
    {
        await using var scope = _provider.CreateAsyncScope();
        await action(scope.ServiceProvider.GetRequiredService<IAuthService>());
    }

    public async Task<T> Db<T>(Func<AppDbContext, Task<T>> action)
    {
        await using var scope = _provider.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    public async Task DbVoid(Func<AppDbContext, Task> action)
    {
        await using var scope = _provider.CreateAsyncScope();
        await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    public string Hash(string token) => _provider.GetRequiredService<ITokenService>().HashRefreshToken(token);

    public ValueTask DisposeAsync() => _provider.DisposeAsync();
}