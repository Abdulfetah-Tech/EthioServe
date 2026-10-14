using EthioServe.Domain.Common;
using EthioServe.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EthioServe.Infrastructure.Persistence.Seed;

/// <summary>
/// Development-only: creates an Admin account when Seed:AdminEmail and Seed:AdminPassword are configured
/// (user-secrets). Production admins are created through a separate, deliberate step, never from config defaults.
/// </summary>
public static class AdminBootstrapper
{
    public static async Task EnsureAdminAsync(IServiceProvider services, IConfiguration configuration)
    {
        var email = configuration["Seed:AdminEmail"];
        var password = configuration["Seed:AdminPassword"];

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        await using var scope = services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("AdminBootstrapper");

        var existing = await users.FindByEmailAsync(email);
        if (existing is not null)
        {
            if (!await users.IsInRoleAsync(existing, Roles.Admin))
            {
                await users.AddToRoleAsync(existing, Roles.Admin);
            }

            return;
        }

        var admin = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true };
        var created = await users.CreateAsync(admin, password);

        if (!created.Succeeded)
        {
            throw new InvalidOperationException(
                $"Could not create the development admin: {string.Join("; ", created.Errors.Select(e => e.Description))}");
        }

        await users.AddToRoleAsync(admin, Roles.Admin);
        logger.LogInformation("Created development admin account {Email}.", email);
    }
}