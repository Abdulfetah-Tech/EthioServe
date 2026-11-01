// Placeholder for payment gateway definitions
namespace EthioServe.Application.Payments
{
    /// <summary>
    /// Represents external payment gateways (e.g., Stripe, PayPal, local mobile‑money).
    /// The real implementation will encapsulate API client configuration.
    /// </summary>
    public interface IPaymentGateway
    {
        /// <summary>Process a payment of the given amount in the specified currency.</summary>
        /// <param name="amount">Payment amount.</param>
        /// <param name="currency">ISO currency code (e.g., "USD").</param>
        /// <returns>True if the payment succeeded.</returns>
        bool Process(decimal amount, string currency);
    }

    /// <summary>
    /// A simple mock gateway used for development and unit tests.
    /// </summary>
    public sealed class MockPaymentGateway : IPaymentGateway
    {
        public bool Process(decimal amount, string currency) => true;
    }
}

