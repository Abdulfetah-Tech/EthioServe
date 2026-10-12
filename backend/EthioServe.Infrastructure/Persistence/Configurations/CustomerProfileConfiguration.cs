using EthioServe.Domain.Entities;
using EthioServe.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EthioServe.Infrastructure.Persistence.Configurations;

public sealed class CustomerProfileConfiguration : IEntityTypeConfiguration<CustomerProfile>
{
    public void Configure(EntityTypeBuilder<CustomerProfile> builder)
    {
        builder.ToTable("customer_profiles");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.FirstName).HasMaxLength(100).IsRequired();
        builder.Property(p => p.LastName).HasMaxLength(100).IsRequired();
        builder.Property(p => p.PhoneNumber).HasMaxLength(20).IsRequired();
        builder.Property(p => p.ProfileImageKey).HasMaxLength(300);
        builder.Property(p => p.Address).HasMaxLength(300);

        builder.HasOne<ApplicationUser>().WithOne().HasForeignKey<CustomerProfile>(p => p.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Region>().WithMany().HasForeignKey(p => p.RegionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<City>().WithMany().HasForeignKey(p => p.CityId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SubCity>().WithMany().HasForeignKey(p => p.SubCityId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(p => p.UserId).IsUnique().HasDatabaseName("ux_customer_profiles_user_id");
    }
}