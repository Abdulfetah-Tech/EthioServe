using EthioServe.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EthioServe.Infrastructure.Persistence.Configurations;

public sealed class SubCityConfiguration : IEntityTypeConfiguration<SubCity>
{
    public void Configure(EntityTypeBuilder<SubCity> builder)
    {
        builder.ToTable("sub_cities");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Name).HasMaxLength(100).IsRequired();
        builder.Property(s => s.NameAm).HasMaxLength(100);

        builder.HasOne<City>().WithMany().HasForeignKey(s => s.CityId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(s => new { s.CityId, s.Name }).IsUnique().HasDatabaseName("ux_sub_cities_city_name");
    }
}