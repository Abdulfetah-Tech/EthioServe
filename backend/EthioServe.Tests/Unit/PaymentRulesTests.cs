using EthioServe.Application.Payments;

namespace EthioServe.Tests.Unit;

public sealed class PaymentRulesTests
{
    [Fact]
    public void Payment_rules_placeholder_test()
    {
        Assert.Equal(0.01m, PaymentRules.MinimumAmount);
    }
}
