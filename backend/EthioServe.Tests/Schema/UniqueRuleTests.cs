using EthioServe.Domain.Entities;
using EthioServe.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace EthioServe.Tests.Schema;

[Collection(DatabaseCollection.Name)]
public sealed class UniqueRuleTests(DatabaseFixture fixture)
{
    [Fact]
    public async Task Only_one_quote_per_request_can_be_accepted()
    {
        await using var db = fixture.CreateContext();
        var world = await TestData.CreateWorldAsync(db);
        var otherProvider = await TestData.AddProviderAsync(db, world);
        var first = await TestData.AddQuoteAsync(db, world);
        var second = await TestData.AddQuoteAsync(db, world, otherProvider);

        await TestData.SetQuoteStatusAsync(db, first.Id, "Accepted");
        var error = await DbAssert.RejectedAsync(() => TestData.SetQuoteStatusAsync(db, second.Id, "Accepted"));

        DbAssert.Violates(error, PostgresErrorCodes.UniqueViolation, "ux_quotes_one_accepted_per_request");
    }

    [Fact]
    public async Task Provider_cannot_have_two_open_quotes_for_one_request()
    {
        await using var db = fixture.CreateContext();
        var world = await TestData.CreateWorldAsync(db);
        await TestData.AddQuoteAsync(db, world);

        await using var other = fixture.CreateContext();
        var error = await DbAssert.RejectedAsync(() => TestData.AddQuoteAsync(other, world));

        DbAssert.Violates(error, PostgresErrorCodes.UniqueViolation, "ux_quotes_one_pending_per_provider");
    }

    [Fact]
    public async Task Provider_can_quote_again_after_the_previous_quote_was_rejected()
    {
        await using var db = fixture.CreateContext();
        var world = await TestData.CreateWorldAsync(db);
        var first = await TestData.AddQuoteAsync(db, world);

        await TestData.SetQuoteStatusAsync(db, first.Id, "Rejected");
        await TestData.AddQuoteAsync(db, world);

        var count = await db.Quotes.CountAsync(q => q.ServiceRequestId == world.RequestId);
        Assert.Equal(2, count);
    }

    [Fact]
    public async Task Only_one_active_appointment_per_request()
    {
        await using var db = fixture.CreateContext();
        var world = await TestData.CreateWorldAsync(db);
        var quote = await TestData.AddQuoteAsync(db, world);
        await TestData.AddAppointmentAsync(db, world, quote);

        await using var other = fixture.CreateContext();
        var error = await DbAssert.RejectedAsync(() => TestData.AddAppointmentAsync(other, world, quote, hoursFromNow: 72));

        DbAssert.Violates(error, PostgresErrorCodes.UniqueViolation, "ux_appointments_one_active_per_request");
    }

    [Fact]
    public async Task Cancelled_appointment_does_not_block_a_new_one()
    {
        await using var db = fixture.CreateContext();
        var world = await TestData.CreateWorldAsync(db);
        var quote = await TestData.AddQuoteAsync(db, world);
        var first = await TestData.AddAppointmentAsync(db, world, quote);

        await TestData.SetAppointmentStatusAsync(db, first.Id, "Cancelled");
        await TestData.AddAppointmentAsync(db, world, quote, hoursFromNow: 72);

        var count = await db.Appointments.CountAsync(a => a.ServiceRequestId == world.RequestId);
        Assert.Equal(2, count);
    }

    [Fact]
    public async Task Only_one_review_per_request()
    {
        await using var db = fixture.CreateContext();
        var world = await TestData.CreateWorldAsync(db);
        await TestData.AddReviewAsync(db, world);

        await using var other = fixture.CreateContext();
        var error = await DbAssert.RejectedAsync(() => TestData.AddReviewAsync(other, world, rating: 3));

        DbAssert.Violates(error, PostgresErrorCodes.UniqueViolation, "ux_reviews_service_request_id");
    }

    [Fact]
    public async Task Provider_cannot_offer_the_same_service_twice()
    {
        await using var db = fixture.CreateContext();
        var world = await TestData.CreateWorldAsync(db);
        db.ProviderServices.Add(new ProviderService(world.ProviderId, world.ServiceId, 300m));
        await db.SaveChangesAsync();

        await using var other = fixture.CreateContext();
        other.ProviderServices.Add(new ProviderService(world.ProviderId, world.ServiceId, 350m));
        var error = await DbAssert.RejectedAsync(() => other.SaveChangesAsync());

        DbAssert.Violates(error, PostgresErrorCodes.UniqueViolation, "ux_provider_services_provider_service");
    }

    [Fact]
    public async Task Transaction_reference_must_be_unique()
    {
        await using var db = fixture.CreateContext();
        var world = await TestData.CreateWorldAsync(db);
        var quote = await TestData.AddQuoteAsync(db, world);
        var reference = $"TX-{Guid.NewGuid():N}";
        await TestData.AddPaymentAsync(db, world, quote, reference);

        await using var other = fixture.CreateContext();
        var error = await DbAssert.RejectedAsync(() => TestData.AddPaymentAsync(other, world, quote, reference));

        DbAssert.Violates(error, PostgresErrorCodes.UniqueViolation, "ux_payments_transaction_reference");
    }

    [Fact]
    public async Task Idempotency_key_must_be_unique()
    {
        await using var db = fixture.CreateContext();
        var world = await TestData.CreateWorldAsync(db);
        var quote = await TestData.AddQuoteAsync(db, world);
        var key = $"KEY-{Guid.NewGuid():N}";
        await TestData.AddPaymentAsync(db, world, quote, $"TX-{Guid.NewGuid():N}", key);

        await using var other = fixture.CreateContext();
        var error = await DbAssert.RejectedAsync(
            () => TestData.AddPaymentAsync(other, world, quote, $"TX-{Guid.NewGuid():N}", key));

        DbAssert.Violates(error, PostgresErrorCodes.UniqueViolation, "ux_payments_idempotency_key");
    }

    [Fact]
    public async Task Only_one_payment_per_request_can_be_completed()
    {
        await using var db = fixture.CreateContext();
        var world = await TestData.CreateWorldAsync(db);
        var quote = await TestData.AddQuoteAsync(db, world);
        var first = await TestData.AddPaymentAsync(db, world, quote, $"TX-{Guid.NewGuid():N}");
        var second = await TestData.AddPaymentAsync(db, world, quote, $"TX-{Guid.NewGuid():N}");

        await TestData.SetPaymentStatusAsync(db, first.Id, "Completed");
        var error = await DbAssert.RejectedAsync(() => TestData.SetPaymentStatusAsync(db, second.Id, "Completed"));

        DbAssert.Violates(error, PostgresErrorCodes.UniqueViolation, "ux_payments_one_completed_per_request");
    }

    [Fact]
    public async Task Email_must_be_unique_regardless_of_letter_case()
    {
        var tag = Guid.NewGuid().ToString("N")[..10];
        await using var db = fixture.CreateContext();
        db.Users.Add(TestData.NewUser($"first-{tag}", $"same-{tag}@test.local"));
        await db.SaveChangesAsync();

        await using var other = fixture.CreateContext();
        other.Users.Add(TestData.NewUser($"second-{tag}", $"SAME-{tag}@test.local"));
        var error = await DbAssert.RejectedAsync(() => other.SaveChangesAsync());

        DbAssert.Violates(error, PostgresErrorCodes.UniqueViolation, "ux_users_normalized_email");
    }
}