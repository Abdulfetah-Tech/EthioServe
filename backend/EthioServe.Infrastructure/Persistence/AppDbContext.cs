using EthioServe.Application.Abstractions;
using EthioServe.Domain.Common;
using EthioServe.Domain.Entities;
using EthioServe.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace EthioServe.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options, IClock clock)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<Region> Regions => Set<Region>();

    public DbSet<City> Cities => Set<City>();

    public DbSet<SubCity> SubCities => Set<SubCity>();

    public DbSet<CustomerProfile> CustomerProfiles => Set<CustomerProfile>();

    public DbSet<ProviderProfile> ProviderProfiles => Set<ProviderProfile>();

    public DbSet<ServiceCategory> ServiceCategories => Set<ServiceCategory>();

    public DbSet<Service> Services => Set<Service>();

    public DbSet<ProviderService> ProviderServices => Set<ProviderService>();

    public DbSet<ProviderDocument> ProviderDocuments => Set<ProviderDocument>();

    public DbSet<ServiceRequest> ServiceRequests => Set<ServiceRequest>();

    public DbSet<RequestAttachment> RequestAttachments => Set<RequestAttachment>();

    public DbSet<Quote> Quotes => Set<Quote>();

    public DbSet<Appointment> Appointments => Set<Appointment>();

    public DbSet<Payment> Payments => Set<Payment>();

    public DbSet<Review> Reviews => Set<Review>();

    public DbSet<Notification> Notifications => Set<Notification>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Identity's own mapping must come first; we then rename its tables to snake_case.
        base.OnModelCreating(modelBuilder);
        ConfigureIdentityTables(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        StampTimestamps();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        StampTimestamps();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <summary>Sets created/updated timestamps from the injected clock so no caller can forge or forget them.</summary>
    private void StampTimestamps()
    {
        var now = clock.UtcNow;

        foreach (var entry in ChangeTracker.Entries())
        {
            switch (entry.State)
            {
                case EntityState.Added when entry.Entity is ICreationAudited created:
                    created.MarkCreated(now);
                    break;
                case EntityState.Modified when entry.Entity is IModificationAudited modified:
                    modified.MarkUpdated(now);
                    break;
            }
        }
    }

    private static void ConfigureIdentityTables(ModelBuilder builder)
    {
        builder.Entity<ApplicationUser>(b =>
        {
            b.ToTable("users");
            b.Property(u => u.PhoneNumber).HasMaxLength(20);
            b.HasIndex(u => u.NormalizedUserName).HasDatabaseName("ux_users_normalized_user_name").IsUnique();
            b.HasIndex(u => u.NormalizedEmail).HasDatabaseName("ux_users_normalized_email").IsUnique();
        });

        builder.Entity<IdentityRole<Guid>>(b =>
        {
            b.ToTable("roles");
            b.HasIndex(r => r.NormalizedName).HasDatabaseName("ux_roles_normalized_name").IsUnique();
        });

        builder.Entity<IdentityUserRole<Guid>>().ToTable("user_roles");
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("user_claims");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("user_logins");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("user_tokens");
        builder.Entity<IdentityRoleClaim<Guid>>().ToTable("role_claims");
    }
}