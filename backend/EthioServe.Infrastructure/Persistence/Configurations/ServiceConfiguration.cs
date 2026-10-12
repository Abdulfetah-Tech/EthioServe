using EthioServe.Domain.Entities;
using EthioServe.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EthioServe.Infrastructure.Persistence.Configurations;

public sealed class ServiceConfiguration : IEntityTypeConfiguration<Service>
{
    public void Configure(EntityTypeBuilder<Service> builder)
    {
        builder.ToTable("services", t =>
        {
            t.HasCheckConstraint("ck_services_base_price", "base_price >= 0");
            t.HasCheckConstraint("ck_services_pricing_type", ConfigurationExtensions.InList<PricingType>("pricing_type"));
        });
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Name).HasMaxLength(150).IsRequired();
        builder.Property(s => s.Description).HasMaxLength(1000);
        builder.Property(s => s.BasePrice).HasPrecision(12, 2);
        builder.Property(s => s.PricingType).AsStringColumn(20);

        builder.HasOne<ServiceCategory>().WithMany().HasForeignKey(s => s.CategoryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(s => new { s.CategoryId, s.Name }).IsUnique().HasDatabaseName("ux_services_category_name");
    }
}