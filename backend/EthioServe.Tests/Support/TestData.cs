using EthioServe.Domain.Entities;
using EthioServe.Domain.Enums;
using EthioServe.Infrastructure.Identity;
using EthioServe.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EthioServe.Tests.Support;

/// <summary>Everything a service request needs to exist.</summary>
public sealed record World(int RegionId, int CityId, Guid CustomerId, Guid ProviderId, Guid ServiceId, Guid RequestId);

/// <summary>Builds valid rows with unique names so tests never collide in the shared database.</summary>
public static class TestData
{
    private static string Tag() => Guid.NewGuid().ToString("N")[..10];

    public static ApplicationUser NewUser(string userName, string email) => new()
    {
        UserName = userName,
        NormalizedUserName = userName.ToUpperInvariant(),
        Email = email,
        NormalizedEmail = email.ToUpperInvariant(),
        SecurityStamp = Guid.NewGuid().ToString()
    };

    public static async Task<World> CreateWorldAsync(AppDbContext db)
    {
        var tag = Tag();

        var region = new Region($"Region-{tag}");
        db.Regions.Add(region);
        await db.SaveChangesAsync();

        var city = new City(region.Id, $"City-{tag}");
        db.Cities.Add(city);
        await db.SaveChangesAsync();

        var customerUser = NewUser($"customer-{tag}", $"customer-{tag}@test.local");
        var providerUser = NewUser($"provider-{tag}", $"provider-{tag}@test.local");
        db.Users.AddRange(customerUser, providerUser);
        await db.SaveChangesAsync();

        var customer = new CustomerProfile(customerUser.Id, "Test", "Customer", "0911000001");
        var provider = new ProviderProfile(providerUser.Id, $"Provider {tag}", "0911000002", region.Id, city.Id);
        var category = new ServiceCategory($"Category-{tag}");
        db.AddRange(customer, provider, category);
        await db.SaveChangesAsync();

        var service = new Service(category.Id, $"Service-{tag}", PricingType.Fixed, 100m);
        db.Services.Add(service);
        await db.SaveChangesAsync();

        var request = new ServiceRequest(
            customer.Id, service.Id, "Leaking pipe", "The kitchen pipe is leaking", "Bole, Addis Ababa",
            region.Id, city.Id);
        db.ServiceRequests.Add(request);
        await db.SaveChangesAsync();

        return new World(region.Id, city.Id, customer.Id, provider.Id, service.Id, request.Id);
    }

    public static async Task<Guid> AddProviderAsync(AppDbContext db, World world)
    {
        var tag = Tag();
        var user = NewUser($"provider-{tag}", $"provider-{tag}@test.local");
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var provider = new ProviderProfile(user.Id, $"Provider {tag}", "0911000003", world.RegionId, world.CityId);
        db.ProviderProfiles.Add(provider);
        await db.SaveChangesAsync();

        return provider.Id;
    }

    public static async Task<Quote> AddQuoteAsync(AppDbContext db, World world, Guid? providerId = null)
    {
        var quote = new Quote(
            world.RequestId, providerId ?? world.ProviderId, 500m, "Replace the pipe", 60, DateTimeOffset.UtcNow.AddDays(3));
        db.Quotes.Add(quote);
        await db.SaveChangesAsync();
        return quote;
    }

    public static async Task<Appointment> AddAppointmentAsync(AppDbContext db, World world, Quote quote, int hoursFromNow = 24)
    {
        var start = DateTimeOffset.UtcNow.AddHours(hoursFromNow);
        var appointment = new Appointment(
            world.RequestId, quote.Id, world.ProviderId, world.CustomerId, start, start.AddHours(2), "Bole, Addis Ababa");
        db.Appointments.Add(appointment);
        await db.SaveChangesAsync();
        return appointment;
    }

    public static async Task<Payment> AddPaymentAsync(
        AppDbContext db, World world, Quote quote, string reference, string? idempotencyKey = null)
    {
        var payment = new Payment(
            world.RequestId, quote.Id, world.CustomerId, world.ProviderId, quote.Amount,
            PaymentMethod.Mock, reference, idempotencyKey);
        db.Payments.Add(payment);
        await db.SaveChangesAsync();
        return payment;
    }

    public static async Task<Review> AddReviewAsync(AppDbContext db, World world, int rating = 5)
    {
        var review = new Review(world.RequestId, world.CustomerId, world.ProviderId, rating);
        db.Reviews.Add(review);
        await db.SaveChangesAsync();
        return review;
    }

    // Entities deliberately have no public status setters yet, so tests move rows between states with SQL.
    // That also proves the rules live in the database, not only in C#.
    public static Task SetQuoteStatusAsync(AppDbContext db, Guid id, string status) =>
        db.Database.ExecuteSqlInterpolatedAsync($"UPDATE quotes SET status = {status} WHERE id = {id}");

    public static Task SetAppointmentStatusAsync(AppDbContext db, Guid id, string status) =>
        db.Database.ExecuteSqlInterpolatedAsync($"UPDATE appointments SET status = {status} WHERE id = {id}");

    public static Task SetPaymentStatusAsync(AppDbContext db, Guid id, string status) =>
        db.Database.ExecuteSqlInterpolatedAsync($"UPDATE payments SET status = {status} WHERE id = {id}");
}