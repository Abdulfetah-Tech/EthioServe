using EthioServe.Application.Abstractions;
using EthioServe.Infrastructure.Identity;
using EthioServe.Infrastructure.Persistence;
using EthioServe.Infrastructure.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EthioServe.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:Default is not configured. Use 'dotnet user-secrets' locally or an environment variable.");

        services.AddDbContext<AppDbContext>(options => options
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention());

        services.AddSingleton<IClock, SystemClock>();

        // Fails fast at startup if the JWT settings are missing or invalid (for example, no signing key).
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<ITokenService, JwtTokenService>();

        return services;
    }
}