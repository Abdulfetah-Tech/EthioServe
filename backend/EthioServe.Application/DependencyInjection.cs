using EthioServe.Application.Auth;
using EthioServe.Application.Catalog;
using EthioServe.Application.Locations;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace EthioServe.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining<RegisterCustomerRequestValidator>();

        services.AddScoped<ILocationQueries, LocationQueries>();
        services.AddScoped<ICatalogQueries, CatalogQueries>();

        return services;
    }
}