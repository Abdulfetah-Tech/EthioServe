namespace EthioServe.Application.Payments;

/// <summary>
/// Represents an external payment gateway implementation.
/// </summary>
public interface IPaymentGateway
{
    bool Process(decimal amount, string currency);
}

/// <summary>
/// A simple mock gateway used for development and unit tests.
/// </summary>
public sealed class MockPaymentGateway : IPaymentGateway
{
    public bool Process(decimal amount, string currency) => true;
}
