using EthioServe.Domain.Entities;
using EthioServe.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EthioServe.Infrastructure.Persistence.Configurations;

public sealed class ServiceRequestConfiguration : IEntityTypeConfiguration<ServiceRequest>
{
    public void Configure(EntityTypeBuilder<ServiceRequest> builder)
    {
        builder.ToTable("service_requests", t =>
        {
            t.HasCheckConstraint("ck_service_requests_budget_min", "budget_min >= 0");
            t.HasCheckConstraint("ck_service_requests_budget_range", "budget_max >= budget_min");
            t.HasCheckConstraint("ck_service_requests_status", ConfigurationExtensions.InList<ServiceRequestStatus>("status"));
        });
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Title).HasMaxLength(150).IsRequired();
        builder.Property(r => r.Description).HasMaxLength(4000).IsRequired();
        builder.Property(r => r.Address).HasMaxLength(300).IsRequired();
        builder.Property(r => r.BudgetMin).HasPrecision(12, 2);
        builder.Property(r => r.BudgetMax).HasPrecision(12, 2);
        builder.Property(r => r.Status).AsStringColumn(20);
        builder.Property(r => r.CancelledReason).HasMaxLength(500);
        builder.Property(r => r.Version).UseXminVersion();

        builder.HasOne<CustomerProfile>().WithMany().HasForeignKey(r => r.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Service>().WithMany().HasForeignKey(r => r.ServiceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Region>().WithMany().HasForeignKey(r => r.RegionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<City>().WithMany().HasForeignKey(r => r.CityId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(r => new { r.CustomerId, r.Status, r.CreatedAt })
            .IsDescending(false, false, true)
            .HasDatabaseName("ix_service_requests_customer_status_created");
        builder.HasIndex(r => new { r.ServiceId, r.CityId, r.Status })
            .HasDatabaseName("ix_service_requests_provider_feed");
    }
}