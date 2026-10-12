using EthioServe.Domain.Common;
using EthioServe.Domain.Entities;
using EthioServe.Domain.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EthioServe.Infrastructure.Persistence.Seed;

/// <summary>Idempotent reference-data seeder: safe to run on every startup, it only inserts what is missing.</summary>
public static class DatabaseSeeder
{
    private sealed record CitySeed(string Name, string? NameAm = null, string[]? SubCities = null);

    private sealed record RegionSeed(string Name, string? NameAm, CitySeed[] Cities);

    private sealed record ServiceSeed(string Name, PricingType Pricing, decimal? BasePrice, string? Description = null);

    private sealed record CategorySeed(string Name, string Description, ServiceSeed[] Services);

    private static readonly RegionSeed[] Locations =
    [
        new("Addis Ababa", "አዲስ አበባ",
        [
            new("Addis Ababa", "አዲስ አበባ",
            [
                "Addis Ketema", "Akaki Kaliti", "Arada", "Bole", "Gullele", "Kirkos",
                "Kolfe Keranio", "Lideta", "Nifas Silk-Lafto", "Yeka", "Lemi Kura"
            ])
        ]),
        new("Oromia", null, [new("Adama"), new("Jimma")]),
        new("Amhara", null, [new("Bahir Dar"), new("Gondar")]),
        new("Tigray", null, [new("Mekelle")]),
        new("Sidama", null, [new("Hawassa")]),
        new("Dire Dawa", null, [new("Dire Dawa")]),
        new("Harari", null, [new("Harar")])
    ];

    // Base prices are illustrative ETB amounts for demo data, not market research.
    private static readonly CategorySeed[] Catalog =
    [
        new("Plumbing", "Pipes, leaks, heaters and bathroom fixtures",
        [
            new("Pipe repair", PricingType.StartingFrom, 300m),
            new("Leak detection", PricingType.Fixed, 400m),
            new("Water heater installation", PricingType.Fixed, 1500m),
            new("Bathroom fixture installation", PricingType.StartingFrom, 600m)
        ]),
        new("Electrical", "Wiring, lighting and power problems",
        [
            new("Wiring and rewiring", PricingType.Negotiable, null),
            new("Lighting installation", PricingType.StartingFrom, 250m),
            new("Power outage troubleshooting", PricingType.Hourly, 250m),
            new("Generator installation", PricingType.Negotiable, null)
        ]),
        new("Cleaning", "Home, office and deep cleaning",
        [
            new("Home cleaning", PricingType.Fixed, 800m),
            new("Office cleaning", PricingType.Negotiable, null),
            new("Deep cleaning", PricingType.StartingFrom, 1500m),
            new("Carpet and sofa cleaning", PricingType.StartingFrom, 700m)
        ]),
        new("Carpentry", "Furniture, doors, windows and cabinets",
        [
            new("Furniture making", PricingType.Negotiable, null),
            new("Door and window repair", PricingType.StartingFrom, 400m),
            new("Kitchen cabinet installation", PricingType.Negotiable, null)
        ]),
        new("Painting", "Interior and exterior painting",
        [
            new("Interior painting", PricingType.StartingFrom, 2000m),
            new("Exterior painting", PricingType.Negotiable, null)
        ]),
        new("Appliance repair", "Washing machines, refrigerators and TVs",
        [
            new("Washing machine repair", PricingType.StartingFrom, 500m),
            new("Refrigerator repair", PricingType.StartingFrom, 600m),
            new("TV repair", PricingType.StartingFrom, 400m)
        ]),
        new("Gardening", "Garden and lawn care",
        [
            new("Garden maintenance", PricingType.Hourly, 200m),
            new("Lawn care", PricingType.Fixed, 500m)
        ]),
        new("Moving and delivery", "House moving and furniture delivery",
        [
            new("House moving", PricingType.Negotiable, null),
            new("Furniture delivery", PricingType.StartingFrom, 700m)
        ]),
        new("IT support", "Computers, laptops and networks",
        [
            new("Laptop and PC repair", PricingType.StartingFrom, 400m),
            new("Network and Wi-Fi setup", PricingType.Fixed, 900m)
        ])
    ];

    public static async Task SeedAsync(AppDbContext db, CancellationToken ct = default)
    {
        await SeedRolesAsync(db, ct);
        await SeedLocationsAsync(db, ct);
        await SeedCatalogAsync(db, ct);
    }

    private static async Task SeedRolesAsync(AppDbContext db, CancellationToken ct)
    {
        var existing = await db.Roles.Select(r => r.NormalizedName).ToListAsync(ct);

        foreach (var name in Roles.All)
        {
            var normalized = name.ToUpperInvariant();

            if (!existing.Contains(normalized))
            {
                db.Roles.Add(new IdentityRole<Guid>(name) { NormalizedName = normalized });
            }
        }

        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedLocationsAsync(AppDbContext db, CancellationToken ct)
    {
        foreach (var regionSeed in Locations)
        {
            var region = await db.Regions.FirstOrDefaultAsync(r => r.Name == regionSeed.Name, ct);

            if (region is null)
            {
                region = new Region(regionSeed.Name, regionSeed.NameAm);
                db.Regions.Add(region);
                await db.SaveChangesAsync(ct); // we need the generated id for the cities below
            }

            foreach (var citySeed in regionSeed.Cities)
            {
                var regionId = region.Id;
                var city = await db.Cities.FirstOrDefaultAsync(c => c.RegionId == regionId && c.Name == citySeed.Name, ct);

                if (city is null)
                {
                    city = new City(regionId, citySeed.Name, citySeed.NameAm);
                    db.Cities.Add(city);
                    await db.SaveChangesAsync(ct);
                }

                foreach (var subCityName in citySeed.SubCities ?? [])
                {
                    var cityId = city.Id;
                    var exists = await db.SubCities.AnyAsync(s => s.CityId == cityId && s.Name == subCityName, ct);

                    if (!exists)
                    {
                        db.SubCities.Add(new SubCity(cityId, subCityName));
                    }
                }
            }
        }

        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedCatalogAsync(AppDbContext db, CancellationToken ct)
    {
        foreach (var categorySeed in Catalog)
        {
            var category = await db.ServiceCategories.FirstOrDefaultAsync(c => c.Name == categorySeed.Name, ct);

            if (category is null)
            {
                category = new ServiceCategory(categorySeed.Name, categorySeed.Description);
                db.ServiceCategories.Add(category);
                await db.SaveChangesAsync(ct);
            }

            foreach (var serviceSeed in categorySeed.Services)
            {
                var categoryId = category.Id;
                var exists = await db.Services.AnyAsync(s => s.CategoryId == categoryId && s.Name == serviceSeed.Name, ct);

                if (!exists)
                {
                    db.Services.Add(new Service(
                        categoryId, serviceSeed.Name, serviceSeed.Pricing, serviceSeed.BasePrice, serviceSeed.Description));
                }
            }
        }

        await db.SaveChangesAsync(ct);
    }
}