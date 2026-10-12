using EthioServe.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EthioServe.Infrastructure.Persistence.Configurations;

public sealed class CityConfiguration : IEntityTypeConfiguration<City>
{
    public void Configure(EntityTypeBuilder<City> builder)
    {
        builder.ToTable("cities");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Name).HasMaxLength(100).IsRequired();
        builder.Property(c => c.NameAm).HasMaxLength(100);

        builder.HasOne<Region>().WithMany().HasForeignKey(c => c.RegionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(c => new { c.RegionId, c.Name }).IsUnique().HasDatabaseName("ux_cities_region_name");
    }
}