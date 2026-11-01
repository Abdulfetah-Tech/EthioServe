namespace EthioServe.Application.Payments;

public interface ICustomerPaymentService
{
    Task<string> GetCustomerBalanceAsync(Guid customerId, CancellationToken ct = default);
}

public sealed class CustomerPaymentService : ICustomerPaymentService
{
    public Task<string> GetCustomerBalanceAsync(Guid customerId, CancellationToken ct = default)
        => Task.FromResult($"{customerId}: 0.00");
}
