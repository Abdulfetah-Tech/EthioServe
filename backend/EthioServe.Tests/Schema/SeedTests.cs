using EthioServe.Infrastructure.Persistence;
using EthioServe.Infrastructure.Persistence.Seed;
using EthioServe.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace EthioServe.Tests.Schema;

[Collection(DatabaseCollection.Name)]
public sealed class SeedTests(DatabaseFixture fixture)
{
    private static async Task<(int Roles, int Regions, int Cities, int SubCities, int Categories, int Services)> CountsAsync(
        AppDbContext db) =>
        (await db.Roles.CountAsync(), await db.Regions.CountAsync(), await db.Cities.CountAsync(),
         await db.SubCities.CountAsync(), await db.ServiceCategories.CountAsync(), await db.Services.CountAsync());

    [Fact]
    public async Task Seeder_is_idempotent()
    {
        await using var db = fixture.CreateContext();

        await DatabaseSeeder.SeedAsync(db);
        var afterFirstRun = await CountsAsync(db);

        await DatabaseSeeder.SeedAsync(db);
        var afterSecondRun = await CountsAsync(db);

        Assert.Equal(afterFirstRun, afterSecondRun);
    }

    [Fact]
    public async Task Seeder_creates_exactly_the_three_roles()
    {
        await using var db = fixture.CreateContext();
        await DatabaseSeeder.SeedAsync(db);

        var names = await db.Roles.Select(r => r.Name!).OrderBy(n => n).ToListAsync();

        Assert.Equal(new[] { "Admin", "Customer", "Provider" }, names);
    }

    [Fact]
    public async Task Seeder_creates_addis_ababa_sub_cities_and_the_plumbing_catalog()
    {
        await using var db = fixture.CreateContext();
        await DatabaseSeeder.SeedAsync(db);

        var addisSubCities = await db.SubCities
            .CountAsync(s => db.Cities.Any(c => c.Id == s.CityId && c.Name == "Addis Ababa"));
        var plumbingServices = await db.Services
            .CountAsync(s => db.ServiceCategories.Any(c => c.Id == s.CategoryId && c.Name == "Plumbing"));

        Assert.Equal(11, addisSubCities);
        Assert.Equal(4, plumbingServices);
    }
}