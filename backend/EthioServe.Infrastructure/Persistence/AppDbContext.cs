using EthioServe.Application.Abstractions;
using EthioServe.Application.Notifications;
using EthioServe.Domain.Common;
using EthioServe.Domain.Entities;
using EthioServe.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace EthioServe.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options, IClock clock, INotificationPublisher? publisher = null)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options), IApplicationDbContext
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
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Identity's own mapping must come first; we then rename its tables to snake_case.
        base.OnModelCreating(modelBuilder);
        ConfigureIdentityTables(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

    /// <summary>Forgets every tracked change, so an operation can start again from what is stored.</summary>
    public void DiscardChanges() => ChangeTracker.Clear();

    /// <summary>
    /// Saves, and then tells the people who are online about the notifications that were just saved. Nothing is pushed
    /// when the save fails, and a failed push never fails the save.
    /// </summary>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var added = publisher is null
            ? new List<Notification>()
            : ChangeTracker.Entries<Notification>().Where(e => e.State == EntityState.Added).Select(e => e.Entity).ToList();

        var result = await base.SaveChangesAsync(cancellationToken);

        if (publisher is not null && added.Count > 0)
        {
#pragma warning disable CA1031 // the push is best effort; the publisher logs its own failures
            try
            {
                await publisher.PublishAsync(
                    added.Select(n => new NotificationPush(
                        n.UserId,
                        new NotificationDto(n.Id, n.Type, n.Title, n.Message, n.IsRead, n.RelatedEntityType, n.RelatedEntityId, n.CreatedAt))).ToList(),
                    CancellationToken.None);
            }
            catch (Exception)
            {
                // The notification is saved. The person sees it the next time the app refreshes.
            }
#pragma warning restore CA1031
        }

        return result;
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