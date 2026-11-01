namespace EthioServe.Application.Payments;

public interface IProviderPaymentService
{
    Task<string> GetProviderBalanceAsync(Guid providerId, CancellationToken ct = default);
}

public sealed class ProviderPaymentService : IProviderPaymentService
{
    public Task<string> GetProviderBalanceAsync(Guid providerId, CancellationToken ct = default)
        => Task.FromResult($"{providerId}: 0.00");
}
