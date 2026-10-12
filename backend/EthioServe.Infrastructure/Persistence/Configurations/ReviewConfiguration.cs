using EthioServe.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EthioServe.Infrastructure.Persistence.Configurations;

public sealed class ReviewConfiguration : IEntityTypeConfiguration<Review>
{
    public void Configure(EntityTypeBuilder<Review> builder)
    {
        builder.ToTable("reviews", t => t.HasCheckConstraint("ck_reviews_rating", "rating >= 1 AND rating <= 5"));
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Comment).HasMaxLength(2000);

        builder.HasOne<ServiceRequest>().WithMany().HasForeignKey(r => r.ServiceRequestId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CustomerProfile>().WithMany().HasForeignKey(r => r.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ProviderProfile>().WithMany().HasForeignKey(r => r.ProviderId).OnDelete(DeleteBehavior.Restrict);

        // One review per service request.
        builder.HasIndex(r => r.ServiceRequestId).IsUnique().HasDatabaseName("ux_reviews_service_request_id");
        builder.HasIndex(r => new { r.ProviderId, r.IsVisible, r.CreatedAt })
            .IsDescending(false, false, true)
            .HasDatabaseName("ix_reviews_provider_visible_created");
    }
}