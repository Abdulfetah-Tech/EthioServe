using EthioServe.Domain.Common;
using EthioServe.Domain.Entities;
using EthioServe.Domain.Enums;

namespace EthioServe.Tests.Unit;

public sealed class EntityRulesTests
{
    private static Quote NewQuote(decimal amount = 500m, int minutes = 60) =>
        new(Guid.NewGuid(), Guid.NewGuid(), amount, "Fix the pipe", minutes, DateTimeOffset.UtcNow.AddDays(3));

    private static ServiceRequest NewRequest(decimal? min = null, decimal? max = null) =>
        new(Guid.NewGuid(), Guid.NewGuid(), "Leaking pipe", "Kitchen pipe is leaking", "Bole, Addis Ababa", 1, 1, min, max);

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Quote_amount_must_be_positive(int amount)
    {
        Assert.Throws<DomainException>(() => NewQuote(amount: amount));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-30)]
    public void Quote_duration_must_be_positive(int minutes)
    {
        Assert.Throws<DomainException>(() => NewQuote(minutes: minutes));
    }

    [Fact]
    public void New_quote_starts_pending()
    {
        var quote = NewQuote();

        Assert.Equal(QuoteStatus.Pending, quote.Status);
        Assert.Equal(500m, quote.Amount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public void Review_rating_outside_1_to_5_is_rejected(int rating)
    {
        Assert.Throws<DomainException>(() => new Review(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), rating));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    public void Review_rating_between_1_and_5_is_accepted(int rating)
    {
        var review = new Review(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), rating);

        Assert.Equal(rating, review.Rating);
        Assert.True(review.IsVisible);
    }

    [Fact]
    public void Appointment_must_end_after_it_starts()
    {
        var start = new DateTimeOffset(2026, 11, 1, 9, 0, 0, TimeSpan.Zero);

        Assert.Throws<DomainException>(() => new Appointment(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), start, start, "Bole, Addis Ababa"));
    }

    [Fact]
    public void Service_request_budget_max_cannot_be_below_min()
    {
        Assert.Throws<DomainException>(() => NewRequest(min: 500m, max: 100m));
    }

    [Fact]
    public void New_service_request_starts_pending_with_optional_budget()
    {
        var request = NewRequest();

        Assert.Equal(ServiceRequestStatus.Pending, request.Status);
        Assert.Null(request.BudgetMin);
    }

    [Fact]
    public void New_payment_uses_etb_and_starts_pending()
    {
        var payment = new Payment(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 750m, PaymentMethod.Mock, "TX-1");

        Assert.Equal("ETB", payment.Currency);
        Assert.Equal(PaymentStatus.Pending, payment.Status);
    }

    [Fact]
    public void Blank_region_name_is_rejected()
    {
        Assert.Throws<DomainException>(() => new Region("   "));
    }

    [Fact]
    public void New_provider_is_unverified_available_and_unrated()
    {
        var provider = new ProviderProfile(Guid.NewGuid(), "Abebe Plumbing", "0911000000", 1, 1);

        Assert.Equal(VerificationStatus.Unverified, provider.VerificationStatus);
        Assert.True(provider.IsAvailable);
        Assert.Equal(0m, provider.AverageRating);
        Assert.Equal(0, provider.TotalReviews);
    }

    [Fact]
    public void Provider_experience_cannot_be_negative()
    {
        Assert.Throws<DomainException>(() =>
            new ProviderProfile(Guid.NewGuid(), "Abebe Plumbing", "0911000000", 1, 1, yearsOfExperience: -1));
    }
}