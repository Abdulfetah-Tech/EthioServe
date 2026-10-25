using EthioServe.Application.Quotes;

namespace EthioServe.Tests.Unit;

public sealed class QuoteValidatorTests
{
    private static readonly SubmitQuoteRequestValidator Validator = new();

    private static SubmitQuoteRequest Valid() => new(500m, "Parts and labour included.", 120, 7);

    private static bool HasError(SubmitQuoteRequest request, string property) =>
        Validator.Validate(request).Errors.Any(e => e.PropertyName == property);

    [Fact]
    public void A_valid_quote_passes()
    {
        Assert.True(Validator.Validate(Valid()).IsValid);
    }

    [Theory]
    [InlineData(0.01, true)]
    [InlineData(500.0, true)]
    [InlineData(10000000.0, true)]
    [InlineData(0.0, false)]
    [InlineData(-5.0, false)]
    [InlineData(12.345, false)]
    [InlineData(10000001.0, false)]
    public void The_amount_must_be_positive_with_at_most_two_decimals(double amount, bool expectedValid)
    {
        Assert.Equal(expectedValid, !HasError(Valid() with { Amount = (decimal)amount }, "Amount"));
    }

    [Theory]
    [InlineData(9, false)]
    [InlineData(10, true)]
    [InlineData(2000, true)]
    [InlineData(2001, false)]
    public void The_description_must_be_10_to_2000_characters(int length, bool expectedValid)
    {
        Assert.Equal(expectedValid, !HasError(Valid() with { Description = new string('x', length) }, "Description"));
    }

    [Fact]
    public void The_description_is_measured_after_trimming()
    {
        Assert.True(HasError(Valid() with { Description = "          x          " }, "Description"));
    }

    [Theory]
    [InlineData(14, false)]
    [InlineData(15, true)]
    [InlineData(43200, true)]
    [InlineData(43201, false)]
    public void The_duration_must_be_15_minutes_to_30_days(int minutes, bool expectedValid)
    {
        Assert.Equal(expectedValid, !HasError(Valid() with { EstimatedDurationMinutes = minutes }, "EstimatedDurationMinutes"));
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(30, true)]
    [InlineData(31, false)]
    public void A_quote_is_valid_for_one_to_thirty_days(int days, bool expectedValid)
    {
        Assert.Equal(expectedValid, !HasError(Valid() with { ValidForDays = days }, "ValidForDays"));
    }
}