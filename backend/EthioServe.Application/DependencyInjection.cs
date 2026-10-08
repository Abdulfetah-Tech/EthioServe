using Microsoft.Extensions.DependencyInjection;

namespace EthioServe.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Application services and validators are registered here from Phase 3 onward.
        return services;
    }
}