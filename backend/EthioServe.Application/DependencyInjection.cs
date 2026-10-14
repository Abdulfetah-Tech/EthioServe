using EthioServe.Application.Auth;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace EthioServe.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining<RegisterCustomerRequestValidator>();

        return services;
    }
}