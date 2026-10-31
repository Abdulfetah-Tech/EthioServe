using EthioServe.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EthioServe.Application.Abstractions;

/// <summary>
/// The slice of the database the application layer may use. Entities are added here as features need them.
/// Implemented by AppDbContext in Infrastructure.
/// </summary>
public interface IApplicationDbContext
{
    DbSet<Region> Regions { get; }

    DbSet<City> Cities { get; }

    DbSet<SubCity> SubCities { get; }

    DbSet<ServiceCategory> ServiceCategories { get; }

    DbSet<Service> Services { get; }

    DbSet<ProviderProfile> ProviderProfiles { get; }

    DbSet<ProviderService> ProviderServices { get; }

    DbSet<ServiceRequest> ServiceRequests { get; }

    DbSet<RequestAttachment> RequestAttachments { get; }

    DbSet<Quote> Quotes { get; }

    DbSet<Appointment> Appointments { get; }

    DbSet<Review> Reviews { get; }

    DbSet<FavoriteProvider> FavoriteProviders { get; }

    DbSet<RequestMessage> RequestMessages { get; }

    DbSet<Notification> Notifications { get; }

    DbSet<CustomerProfile> CustomerProfiles { get; }

    DbSet<Payment> Payments { get; }

    DbSet<ProviderDocument> ProviderDocuments { get; }

    /// <summary>Forgets every tracked change, so an operation can start again from what is stored.</summary>
    void DiscardChanges();

    /// <summary>
    /// Runs the operation in one database transaction after taking a lock for the key. Operations that use the same
    /// key run one at a time, so they cannot race each other. The lock is released when the transaction ends.
    /// </summary>
    Task<T> WithExclusiveLockAsync<T>(string key, Func<Task<T>> operation, CancellationToken cancellationToken = default);
    DbSet<AuditLog> AuditLogs { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
