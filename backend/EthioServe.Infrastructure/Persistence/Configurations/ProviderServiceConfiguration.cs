using EthioServe.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EthioServe.Infrastructure.Persistence.Configurations;

public sealed class ProviderServiceConfiguration : IEntityTypeConfiguration<ProviderService>
{
    public void Configure(EntityTypeBuilder<ProviderService> builder)
    {
        builder.ToTable("provider_services", t => t.HasCheckConstraint("ck_provider_services_price", "price >= 0"));
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Price).HasPrecision(12, 2);
        builder.Property(p => p.Description).HasMaxLength(1000);

        builder.HasOne<ProviderProfile>().WithMany().HasForeignKey(p => p.ProviderId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Service>().WithMany().HasForeignKey(p => p.ServiceId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(p => new { p.ProviderId, p.ServiceId }).IsUnique().HasDatabaseName("ux_provider_services_provider_service");
        builder.HasIndex(p => p.ServiceId).HasDatabaseName("ix_provider_services_service_id");
    }
}