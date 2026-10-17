using EthioServe.Application.Providers;

namespace EthioServe.Tests.Unit;

public sealed class ReviewDecisionValidatorTests
{
    private static readonly ReviewDecisionRequestValidator Validator = new();

    [Theory]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("Documents are unclear", true)]
    public void A_reason_is_required(string reason, bool expectedValid)
    {
        Assert.Equal(expectedValid, Validator.Validate(new ReviewDecisionRequest(reason)).IsValid);
    }

    [Fact]
    public void A_reason_is_limited_to_500_characters()
    {
        Assert.True(Validator.Validate(new ReviewDecisionRequest(new string('x', 500))).IsValid);
        Assert.False(Validator.Validate(new ReviewDecisionRequest(new string('x', 501))).IsValid);
    }
}