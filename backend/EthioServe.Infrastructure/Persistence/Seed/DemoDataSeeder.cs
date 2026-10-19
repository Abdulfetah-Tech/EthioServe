using EthioServe.Application.Abstractions;
using EthioServe.Domain.Common;
using EthioServe.Domain.Entities;
using EthioServe.Domain.Enums;
using EthioServe.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EthioServe.Infrastructure.Persistence.Seed;

/// <summary>
/// Development-only marketplace data: fictional providers in several cities, in every verification state, plus one
/// customer. Enabled only when Seed:DemoPassword is configured (user-secrets). Safe to run repeatedly.
/// All names, phone numbers and addresses are made up.
/// </summary>
public static class DemoDataSeeder
{
    public const string EmailDomain = "ethioserve.local";

    public const string CustomerEmail = "demo.customer@" + EmailDomain;

    private sealed record DemoOffer(string Service, decimal Price);

    private sealed record DemoProvider(
        string Slug,
        string BusinessName,
        string Region,
        string City,
        string? SubCity,
        int Years,
        string Description,
        string Address,
        bool Available,
        VerificationStatus Status,
        decimal Rating,
        int Reviews,
        string? Note,
        DemoOffer[] Offers)
    {
        public string Email => $"demo.{Slug}@{EmailDomain}";
    }

    private static readonly DemoProvider[] Providers =
    [
        new("abebe-plumbing", "Abebe Plumbing Services", "Addis Ababa", "Addis Ababa", "Bole", 12,
            "Licensed plumber with over a decade of experience in homes, shops and small offices across the city.",
            "Bole Road", true, VerificationStatus.Verified, 4.8m, 64, null,
            [new("Pipe repair", 350m), new("Leak detection", 450m), new("Water heater installation", 1800m), new("Bathroom fixture installation", 700m)]),

        new("selam-electric", "Selam Electric Works", "Addis Ababa", "Addis Ababa", "Kirkos", 9,
            "Certified electricians for wiring, lighting and generator installation. Safe work, clear quotes.",
            "Kirkos Main Street", true, VerificationStatus.Verified, 4.6m, 41, null,
            [new("Wiring and rewiring", 2500m), new("Lighting installation", 300m), new("Power outage troubleshooting", 280m), new("Generator installation", 6000m)]),

        new("tigist-cleaning", "Tigist Cleaning Co.", "Addis Ababa", "Addis Ababa", "Yeka", 6,
            "Reliable home and office cleaning with our own trained staff and supplies. Deep cleaning is our speciality.",
            "Yeka Sub-city", true, VerificationStatus.Verified, 4.9m, 88, null,
            [new("Home cleaning", 900m), new("Deep cleaning", 1700m), new("Carpet and sofa cleaning", 800m), new("Office cleaning", 3500m)]),

        new("dawit-carpentry", "Dawit Carpentry", "Addis Ababa", "Addis Ababa", "Lideta", 15,
            "Custom furniture, doors, windows and kitchen cabinets made to measure in our own workshop.",
            "Lideta Workshop Area", true, VerificationStatus.Verified, 4.5m, 29, null,
            [new("Furniture making", 5000m), new("Door and window repair", 450m), new("Kitchen cabinet installation", 8000m)]),

        new("yonas-painters", "Yonas Painters", "Addis Ababa", "Addis Ababa", "Nifas Silk-Lafto", 8,
            "Interior and exterior painting with clean finishes and proper surface preparation.",
            "Nifas Silk Road", true, VerificationStatus.Verified, 4.2m, 17, null,
            [new("Interior painting", 2200m), new("Exterior painting", 4500m)]),

        new("kirkos-appliance", "Kirkos Appliance Repair", "Addis Ababa", "Addis Ababa", "Kirkos", 7,
            "Washing machines, refrigerators and televisions repaired at your home, with a short warranty on parts.",
            "Kirkos Service Street", true, VerificationStatus.Verified, 4.0m, 23, null,
            [new("Washing machine repair", 550m), new("Refrigerator repair", 650m), new("TV repair", 450m)]),

        new("hana-it", "Hana IT Solutions", "Addis Ababa", "Addis Ababa", "Bole", 5,
            "Laptop and desktop repair, and Wi-Fi or office network setup for homes and small businesses.",
            "Bole Medhanialem", true, VerificationStatus.Verified, 4.7m, 35, null,
            [new("Laptop and PC repair", 450m), new("Network and Wi-Fi setup", 1000m)]),

        new("lemlem-gardens", "Lemlem Gardens", "Addis Ababa", "Addis Ababa", "Gullele", 10,
            "Garden design, lawn care and regular maintenance for compounds and small estates.",
            "Gullele Garden Road", true, VerificationStatus.Verified, 3.8m, 12, null,
            [new("Garden maintenance", 220m), new("Lawn care", 550m)]),

        new("yeka-movers", "Yeka Movers", "Addis Ababa", "Addis Ababa", "Yeka", 4,
            "House and office moving with careful handling and our own trucks. Currently fully booked.",
            "Yeka Transport Area", false, VerificationStatus.Verified, 3.5m, 9, null,
            [new("House moving", 6000m), new("Furniture delivery", 750m)]),

        new("rahel-homecare", "Rahel Home Care", "Addis Ababa", "Addis Ababa", "Bole", 3,
            "New to EthioServe: friendly, careful home cleaning with eco-friendly products.",
            "Bole Bulbula", true, VerificationStatus.Verified, 0m, 0, null,
            [new("Home cleaning", 850m), new("Deep cleaning", 1600m)]),

        new("adama-builders", "Adama Builders Hub", "Oromia", "Adama", null, 11,
            "General builders in Adama: doors, windows, painting and made-to-order furniture.",
            "Adama Town Center", true, VerificationStatus.Verified, 4.4m, 19, null,
            [new("Door and window repair", 400m), new("Interior painting", 2000m), new("Furniture making", 4200m)]),

        new("hawassa-fixit", "Hawassa Fix-It", "Sidama", "Hawassa", null, 6,
            "Plumbing, wiring and appliance repair for homes and guest houses around Hawassa.",
            "Hawassa Lakeside", true, VerificationStatus.Verified, 4.1m, 14, null,
            [new("Pipe repair", 300m), new("Wiring and rewiring", 2200m), new("Washing machine repair", 500m)]),

        new("bahirdar-electricians", "Bahir Dar Electricians", "Amhara", "Bahir Dar", null, 13,
            "Experienced electricians for new buildings, repairs and backup power in Bahir Dar.",
            "Bahir Dar Main Road", true, VerificationStatus.Verified, 4.6m, 27, null,
            [new("Wiring and rewiring", 2300m), new("Lighting installation", 280m), new("Generator installation", 5500m)]),

        new("mekonnen-pipes", "Mekonnen Pipes and Drains", "Addis Ababa", "Addis Ababa", "Arada", 5,
            "Pipe repair and drain clearing. Waiting for verification by the EthioServe team.",
            "Arada Market Area", true, VerificationStatus.Pending, 0m, 0, null,
            [new("Pipe repair", 320m)]),

        new("sara-quick-electric", "Sara Quick Electric", "Addis Ababa", "Addis Ababa", "Akaki Kaliti", 2,
            "Small electrical jobs and lighting installation.",
            "Akaki Kaliti Road", true, VerificationStatus.Rejected, 0m, 0,
            "Please add a clearer description of your services and your experience.",
            [new("Lighting installation", 250m)]),

        new("habesha-express-movers", "Habesha Express Movers", "Addis Ababa", "Addis Ababa", "Kolfe Keranio", 8,
            "House moving across the city.",
            "Kolfe Keranio", true, VerificationStatus.Suspended, 2.9m, 6,
            "Suspended after repeated customer complaints about missed appointments.",
            [new("House moving", 5800m)]),

        new("fresh-start-cleaning", "Fresh Start Cleaning", "Addis Ababa", "Addis Ababa", null, 1,
            string.Empty, string.Empty, true, VerificationStatus.Unverified, 0m, 0, null, [])
    ];

    /// <summary>How many demo providers exist (used by tests, so no magic numbers).</summary>
    public static int ProviderCount => Providers.Length;

    public static async Task SeedAsync(IServiceProvider services, IConfiguration configuration, CancellationToken ct = default)
    {
        var password = configuration["Seed:DemoPassword"];

        if (string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DemoDataSeeder");

        var regions = await db.Regions.AsNoTracking().ToDictionaryAsync(r => r.Name, r => r.Id, ct);
        var serviceIds = (await db.Services.AsNoTracking().Select(s => new { s.Name, s.Id }).ToListAsync(ct))
            .GroupBy(s => s.Name)
            .ToDictionary(g => g.Key, g => g.First().Id);

        var created = 0;
        var number = 0;

        foreach (var demo in Providers)
        {
            number++;

            if (await users.FindByEmailAsync(demo.Email) is not null)
            {
                continue;
            }

            if (!regions.TryGetValue(demo.Region, out var regionId))
            {
                logger.LogWarning("Skipping {Provider}: region {Region} is not seeded.", demo.BusinessName, demo.Region);
                continue;
            }

            var cityId = await db.Cities.AsNoTracking()
                .Where(c => c.RegionId == regionId && c.Name == demo.City)
                .Select(c => (int?)c.Id)
                .FirstOrDefaultAsync(ct);

            if (cityId is null)
            {
                logger.LogWarning("Skipping {Provider}: city {City} is not seeded.", demo.BusinessName, demo.City);
                continue;
            }

            int? subCityId = null;
            if (demo.SubCity is not null)
            {
                subCityId = await db.SubCities.AsNoTracking()
                    .Where(s => s.CityId == cityId && s.Name == demo.SubCity)
                    .Select(s => (int?)s.Id)
                    .FirstOrDefaultAsync(ct);
            }

            // One transaction per provider: a failure never leaves a login without a profile.
            await using var transaction = await db.Database.BeginTransactionAsync(ct);

            var phone = $"0911{number:D6}";
            var user = new ApplicationUser
            {
                UserName = demo.Email,
                Email = demo.Email,
                EmailConfirmed = true,
                PhoneNumber = phone
            };

            Require(await users.CreateAsync(user, password), demo.Email);
            Require(await users.AddToRoleAsync(user, Roles.Provider), demo.Email);

            var profile = new ProviderProfile(
                user.Id, demo.BusinessName, phone, regionId, cityId.Value, subCityId,
                demo.Description, demo.Address, demo.Years);

            profile.SetAvailability(demo.Available);

            if (demo.Reviews > 0)
            {
                profile.ApplyRatingSummary(demo.Rating, demo.Reviews);
            }

            ApplyStatus(profile, demo, clock.UtcNow);

            db.ProviderProfiles.Add(profile);
            await db.SaveChangesAsync(ct);

            foreach (var offer in demo.Offers)
            {
                if (!serviceIds.TryGetValue(offer.Service, out var serviceId))
                {
                    logger.LogWarning("Skipping offer {Service} of {Provider}: service is not in the catalog.", offer.Service, demo.BusinessName);
                    continue;
                }

                db.ProviderServices.Add(new ProviderService(profile.Id, serviceId, offer.Price));
            }

            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            created++;
        }

        if (await users.FindByEmailAsync(CustomerEmail) is null)
        {
            await CreateCustomerAsync(db, users, password, regions, ct);
            created++;
        }

        if (created > 0)
        {
            logger.LogInformation("Created {Count} demo accounts. Sign in with any demo.*@{Domain} address and the Seed:DemoPassword secret.", created, EmailDomain);
        }
    }

    private static async Task CreateCustomerAsync(
        AppDbContext db, UserManager<ApplicationUser> users, string password,
        IReadOnlyDictionary<string, int> regions, CancellationToken ct)
    {
        int? regionId = regions.TryGetValue("Addis Ababa", out var id) ? id : null;
        var cityId = regionId is null
            ? null
            : await db.Cities.AsNoTracking()
                .Where(c => c.RegionId == regionId && c.Name == "Addis Ababa")
                .Select(c => (int?)c.Id)
                .FirstOrDefaultAsync(ct);

        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var user = new ApplicationUser
        {
            UserName = CustomerEmail,
            Email = CustomerEmail,
            EmailConfirmed = true,
            PhoneNumber = "0911999999"
        };

        Require(await users.CreateAsync(user, password), CustomerEmail);
        Require(await users.AddToRoleAsync(user, Roles.Customer), CustomerEmail);

        db.CustomerProfiles.Add(new CustomerProfile(
            user.Id, "Demo", "Customer", "0911999999", regionId, cityId, null, "Bole, Addis Ababa"));

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    private static void ApplyStatus(ProviderProfile profile, DemoProvider demo, DateTimeOffset now)
    {
        switch (demo.Status)
        {
            case VerificationStatus.Pending:
                profile.RequestVerification();
                break;
            case VerificationStatus.Verified:
                profile.RequestVerification();
                profile.Approve(now);
                break;
            case VerificationStatus.Rejected:
                profile.RequestVerification();
                profile.Reject(demo.Note ?? "Details are unclear.", now);
                break;
            case VerificationStatus.Suspended:
                profile.RequestVerification();
                profile.Approve(now);
                profile.Suspend(demo.Note ?? "Under review.", now);
                break;
        }
    }

    private static void Require(IdentityResult result, string email)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Could not create demo account {email}: {string.Join("; ", result.Errors.Select(e => e.Description))}");
        }
    }
}