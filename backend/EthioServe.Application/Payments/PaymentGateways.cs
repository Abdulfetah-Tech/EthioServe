namespace EthioServe.Application.Payments;

/// <summary>
/// Registry metadata for supported payment gateways.
/// </summary>
public static class PaymentGatewayRegistry
{
    public const string Mock = "mock";

    public static IReadOnlyList<string> Supported => new[] { Mock };
}

