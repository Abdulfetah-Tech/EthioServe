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

    DbSet<CustomerProfile> CustomerProfiles { get; }

    /// <summary>Forgets every tracked change, so an operation can start again from what is stored.</summary>
    void DiscardChanges();
    DbSet<AuditLog> AuditLogs { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}