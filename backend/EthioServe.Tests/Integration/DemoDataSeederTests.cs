using EthioServe.Application.Auth;
using EthioServe.Application.Providers;
using EthioServe.Domain.Enums;
using EthioServe.Infrastructure.Persistence.Seed;
using EthioServe.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace EthioServe.Tests.Integration;

[Collection(DatabaseCollection.Name)]
public sealed class DemoDataSeederTests(DatabaseFixture fixture) : IAsyncLifetime
{
    private const string DemoPassword = "Demo-1Passw0rd-test";

    private AuthHarness _auth = null!;

    public async Task InitializeAsync() => _auth = await AuthHarness.CreateAsync(fixture);

    public async Task DisposeAsync() => await _auth.DisposeAsync();

    private static IConfiguration ConfigWith(string? password) => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { ["Seed:DemoPassword"] = password })
        .Build();

    private Task SeedAsync() => DemoDataSeeder.SeedAsync(_auth.Services, ConfigWith(DemoPassword));

    private sealed record DemoRow(string Name, VerificationStatus Status, decimal Rating, int Reviews, bool Available, int Offers);

    private Task<List<DemoRow>> DemoProvidersAsync() =>
        _auth.Db(async db =>
        {
            var rows = await (
                from p in db.ProviderProfiles.AsNoTracking()
                join u in db.Users.AsNoTracking() on p.UserId equals u.Id
                where u.Email!.StartsWith("demo.")
                select new
                {
                    p.BusinessName,
                    p.VerificationStatus,
                    p.AverageRating,
                    p.TotalReviews,
                    p.IsAvailable,
                    Offers = db.ProviderServices.Count(o => o.ProviderId == p.Id)
                }).ToListAsync();

            return rows
                .Select(r => new DemoRow(r.BusinessName, r.VerificationStatus, r.AverageRating, r.TotalReviews, r.IsAvailable, r.Offers))
                .ToList();
        });

    [Fact]
    public async Task Nothing_is_created_without_a_demo_password()
    {
        var before = await _auth.Db(db => db.Users.CountAsync());

        await DemoDataSeeder.SeedAsync(_auth.Services, ConfigWith(null));
        await DemoDataSeeder.SeedAsync(_auth.Services, ConfigWith("   "));

        Assert.Equal(before, await _auth.Db(db => db.Users.CountAsync()));
    }

    [Fact]
    public async Task Seeding_creates_every_demo_provider_and_one_customer()
    {
        await SeedAsync();

        var providers = await DemoProvidersAsync();
        var customers = await _auth.Db(db => db.Users.CountAsync(u => u.Email == DemoDataSeeder.CustomerEmail));
        var customerProfiles = await _auth.Db(db => db.CustomerProfiles.CountAsync(c => c.FirstName == "Demo" && c.LastName == "Customer"));

        Assert.Equal(DemoDataSeeder.ProviderCount, providers.Count);
        Assert.Equal(1, customers);
        Assert.Equal(1, customerProfiles);
    }

    [Fact]
    public async Task Seeding_twice_creates_no_duplicates()
    {
        await SeedAsync();
        var first = (await DemoProvidersAsync()).Count;
        var firstUsers = await _auth.Db(db => db.Users.CountAsync(u => u.Email!.StartsWith("demo.")));

        await SeedAsync();

        Assert.Equal(first, (await DemoProvidersAsync()).Count);
        Assert.Equal(firstUsers, await _auth.Db(db => db.Users.CountAsync(u => u.Email!.StartsWith("demo."))));
    }

    [Fact]
    public async Task The_demo_providers_cover_every_verification_status()
    {
        await SeedAsync();

        var byStatus = (await DemoProvidersAsync())
            .GroupBy(p => p.Status)
            .ToDictionary(g => g.Key, g => g.Count());

        Assert.Equal(13, byStatus[VerificationStatus.Verified]);
        Assert.Equal(1, byStatus[VerificationStatus.Pending]);
        Assert.Equal(1, byStatus[VerificationStatus.Rejected]);
        Assert.Equal(1, byStatus[VerificationStatus.Suspended]);
        Assert.Equal(1, byStatus[VerificationStatus.Unverified]);
    }

    [Fact]
    public async Task Public_search_lists_only_the_verified_demo_providers_with_the_best_rated_first()
    {
        await SeedAsync();
        var demo = await DemoProvidersAsync();

        var result = await _auth.Db(db => new ProviderSearchQueries(db).SearchAsync(new ProviderSearchQuery { PageSize = 50 }));

        var listed = result.Items.Select(i => i.BusinessName).ToList();
        var verified = demo.Where(d => d.Status == VerificationStatus.Verified).Select(d => d.Name).ToList();
        var hidden = demo.Where(d => d.Status != VerificationStatus.Verified).Select(d => d.Name).ToList();

        Assert.All(verified, name => Assert.Contains(name, listed));
        Assert.All(hidden, name => Assert.DoesNotContain(name, listed));
        Assert.Equal("Tigist Cleaning Co.", listed.First());
    }

    [Fact]
    public async Task Ratings_availability_and_offers_are_set_as_designed()
    {
        await SeedAsync();
        var demo = (await DemoProvidersAsync()).ToDictionary(d => d.Name);

        var tigist = demo["Tigist Cleaning Co."];
        Assert.Equal(4.9m, tigist.Rating);
        Assert.Equal(88, tigist.Reviews);

        var rahel = demo["Rahel Home Care"];
        Assert.Equal(0m, rahel.Rating);
        Assert.Equal(0, rahel.Reviews);

        Assert.False(demo["Yeka Movers"].Available);
        Assert.Equal(4, demo["Abebe Plumbing Services"].Offers);
        Assert.Equal(0, demo["Fresh Start Cleaning"].Offers);
    }

    [Fact]
    public async Task Demo_accounts_can_sign_in_with_the_demo_password()
    {
        await SeedAsync();

        var provider = await _auth.Call(a => a.LoginAsync(
            new LoginRequest("demo.abebe-plumbing@ethioserve.local", DemoPassword), AuthHarness.Client));
        var customer = await _auth.Call(a => a.LoginAsync(
            new LoginRequest(DemoDataSeeder.CustomerEmail, DemoPassword), AuthHarness.Client));

        Assert.Equal(new[] { "Provider" }, provider.User.Roles);
        Assert.NotNull(provider.User.ProviderId);
        Assert.Equal(new[] { "Customer" }, customer.User.Roles);
        Assert.NotNull(customer.User.CustomerId);
    }
}