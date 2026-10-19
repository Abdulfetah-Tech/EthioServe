using EthioServe.Domain.Common;
using EthioServe.Domain.Entities;

namespace EthioServe.Tests.Unit;

public sealed class ProviderRatingTests
{
    private static ProviderProfile NewProvider() =>
        new(Guid.NewGuid(), "Abebe Plumbing", "0911556677", 1, 1);

    [Fact]
    public void A_valid_summary_is_stored_and_rounded_to_two_decimals()
    {
        var provider = NewProvider();

        provider.ApplyRatingSummary(4.666m, 64);

        Assert.Equal(4.67m, provider.AverageRating);
        Assert.Equal(64, provider.TotalReviews);
    }

    [Theory]
    [InlineData(-0.1, 5)]
    [InlineData(5.01, 5)]
    [InlineData(4.0, -1)]
    [InlineData(4.0, 0)]
    public void Invalid_summaries_are_rejected_and_change_nothing(double average, int reviews)
    {
        var provider = NewProvider();
        provider.ApplyRatingSummary(3.5m, 10);

        Assert.Throws<DomainException>(() => provider.ApplyRatingSummary((decimal)average, reviews));

        Assert.Equal(3.5m, provider.AverageRating);
        Assert.Equal(10, provider.TotalReviews);
    }

    [Fact]
    public void A_provider_can_be_reset_to_no_reviews()
    {
        var provider = NewProvider();
        provider.ApplyRatingSummary(4.5m, 20);

        provider.ApplyRatingSummary(0m, 0);

        Assert.Equal(0m, provider.AverageRating);
        Assert.Equal(0, provider.TotalReviews);
    }
}