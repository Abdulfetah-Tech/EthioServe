using EthioServe.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EthioServe.Infrastructure.Persistence.Configurations;

public sealed class RequestAttachmentConfiguration : IEntityTypeConfiguration<RequestAttachment>
{
    public void Configure(EntityTypeBuilder<RequestAttachment> builder)
    {
        builder.ToTable("request_attachments", t => t.HasCheckConstraint("ck_request_attachments_size", "size_bytes > 0"));
        builder.HasKey(a => a.Id);

        builder.Property(a => a.CreatedAt).HasColumnName("uploaded_at");
        builder.Property(a => a.StorageKey).HasMaxLength(300).IsRequired();
        builder.Property(a => a.OriginalFileName).HasMaxLength(255).IsRequired();
        builder.Property(a => a.ContentType).HasMaxLength(100).IsRequired();

        builder.HasOne<ServiceRequest>().WithMany().HasForeignKey(a => a.ServiceRequestId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(a => a.ServiceRequestId).HasDatabaseName("ix_request_attachments_request_id");
    }
}