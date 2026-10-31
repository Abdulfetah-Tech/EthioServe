using EthioServe.Domain.Entities;
using EthioServe.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EthioServe.Infrastructure.Persistence.Configurations;

public sealed class RequestMessageConfiguration : IEntityTypeConfiguration<RequestMessage>
{
    public void Configure(EntityTypeBuilder<RequestMessage> builder)
    {
        builder.ToTable("request_messages");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Body).HasMaxLength(RequestMessage.MaxBodyLength).IsRequired();

        builder.HasOne<ServiceRequest>().WithMany().HasForeignKey(m => m.ServiceRequestId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<ProviderProfile>().WithMany().HasForeignKey(m => m.ProviderId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(m => m.SenderUserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(m => new { m.ServiceRequestId, m.ProviderId, m.CreatedAt })
            .IsDescending(false, false, true)
            .HasDatabaseName("ix_request_messages_request_provider_created");
    }
}
