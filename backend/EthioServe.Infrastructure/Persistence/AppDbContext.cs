using Microsoft.EntityFrameworkCore;

namespace EthioServe.Infrastructure.Persistence;

// Empty for now. Phase 2 adds entities; Phase 3 switches the base class to IdentityDbContext.
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}