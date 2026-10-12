using EthioServe.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace EthioServe.Tests.Schema;

/// <summary>
/// The domain constructors already reject these values. Here we bypass C# with raw SQL to prove the
/// database refuses them too (defense in depth).
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class CheckRuleTests(DatabaseFixture fixture)
{
    [Fact]
    public async Task Quote_amount_must_be_positive()
    {
        await using var db = fixture.CreateContext();
        var world = await TestData.CreateWorldAsync(db);
        var quote = await TestData.AddQuoteAsync(db, world);

        var error = await DbAssert.RejectedAsync(() =>
            db.Database.ExecuteSqlInterpolatedAsync($"UPDATE quotes SET amount = 0 WHERE id = {quote.Id}"));

        DbAssert.Violates(error, PostgresErrorCodes.CheckViolation, "ck_quotes_amount");
    }

    [Fact]
    public async Task Appointment_must_end_after_it_starts()
    {
        await using var db = fixture.CreateContext();
        var world = await TestData.CreateWorldAsync(db);
        var quote = await TestData.AddQuoteAsync(db, world);
        var appointment = await TestData.AddAppointmentAsync(db, world, quote);

        var error = await DbAssert.RejectedAsync(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE appointments SET end_at = start_at - interval '1 hour' WHERE id = {appointment.Id}"));

        DbAssert.Violates(error, PostgresErrorCodes.CheckViolation, "ck_appointments_time_range");
    }

    [Fact]
    public async Task Review_rating_must_be_between_1_and_5()
    {
        await using var db = fixture.CreateContext();
        var world = await TestData.CreateWorldAsync(db);
        var review = await TestData.AddReviewAsync(db, world);

        var error = await DbAssert.RejectedAsync(() =>
            db.Database.ExecuteSqlInterpolatedAsync($"UPDATE reviews SET rating = 6 WHERE id = {review.Id}"));

        DbAssert.Violates(error, PostgresErrorCodes.CheckViolation, "ck_reviews_rating");
    }

    [Fact]
    public async Task Request_status_must_be_a_known_value()
    {
        await using var db = fixture.CreateContext();
        var world = await TestData.CreateWorldAsync(db);

        var error = await DbAssert.RejectedAsync(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE service_requests SET status = 'Bogus' WHERE id = {world.RequestId}"));

        DbAssert.Violates(error, PostgresErrorCodes.CheckViolation, "ck_service_requests_status");
    }

    [Fact]
    public async Task Request_budget_max_cannot_be_below_min()
    {
        await using var db = fixture.CreateContext();
        var world = await TestData.CreateWorldAsync(db);

        var error = await DbAssert.RejectedAsync(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE service_requests SET budget_min = 500, budget_max = 100 WHERE id = {world.RequestId}"));

        DbAssert.Violates(error, PostgresErrorCodes.CheckViolation, "ck_service_requests_budget_range");
    }

    [Fact]
    public async Task Provider_rating_cannot_exceed_5()
    {
        await using var db = fixture.CreateContext();
        var world = await TestData.CreateWorldAsync(db);

        var error = await DbAssert.RejectedAsync(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE provider_profiles SET average_rating = 5.5 WHERE id = {world.ProviderId}"));

        DbAssert.Violates(error, PostgresErrorCodes.CheckViolation, "ck_provider_profiles_rating");
    }

    [Fact]
    public async Task Payment_amount_must_be_positive()
    {
        await using var db = fixture.CreateContext();
        var world = await TestData.CreateWorldAsync(db);
        var quote = await TestData.AddQuoteAsync(db, world);
        var payment = await TestData.AddPaymentAsync(db, world, quote, $"TX-{Guid.NewGuid():N}");

        var error = await DbAssert.RejectedAsync(() =>
            db.Database.ExecuteSqlInterpolatedAsync($"UPDATE payments SET amount = 0 WHERE id = {payment.Id}"));

        DbAssert.Violates(error, PostgresErrorCodes.CheckViolation, "ck_payments_amount");
    }
}