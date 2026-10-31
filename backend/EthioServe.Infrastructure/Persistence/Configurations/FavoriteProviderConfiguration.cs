using EthioServe.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EthioServe.Infrastructure.Persistence.Configurations;

public sealed class FavoriteProviderConfiguration : IEntityTypeConfiguration<FavoriteProvider>
{
    public void Configure(EntityTypeBuilder<FavoriteProvider> builder)
    {
        builder.ToTable("favorite_providers");
        builder.HasKey(f => f.Id);

        builder.HasOne<CustomerProfile>().WithMany().HasForeignKey(f => f.CustomerId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<ProviderProfile>().WithMany().HasForeignKey(f => f.ProviderId).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(f => new { f.CustomerId, f.ProviderId })
            .IsUnique()
            .HasDatabaseName("ux_favorite_providers_customer_provider");
        builder.HasIndex(f => f.ProviderId).HasDatabaseName("ix_favorite_providers_provider_id");
    }
}
