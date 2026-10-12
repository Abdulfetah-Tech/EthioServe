using EthioServe.Domain.Entities;
using EthioServe.Domain.Enums;
using EthioServe.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EthioServe.Infrastructure.Persistence.Configurations;

public sealed class ProviderDocumentConfiguration : IEntityTypeConfiguration<ProviderDocument>
{
    public void Configure(EntityTypeBuilder<ProviderDocument> builder)
    {
        builder.ToTable("provider_documents", t =>
        {
            t.HasCheckConstraint("ck_provider_documents_size", "size_bytes > 0");
            t.HasCheckConstraint("ck_provider_documents_type", ConfigurationExtensions.InList<DocumentType>("document_type"));
            t.HasCheckConstraint("ck_provider_documents_status", ConfigurationExtensions.InList<DocumentStatus>("status"));
        });
        builder.HasKey(d => d.Id);

        builder.Property(d => d.CreatedAt).HasColumnName("uploaded_at");
        builder.Property(d => d.DocumentType).AsStringColumn(30);
        builder.Property(d => d.Status).AsStringColumn(20);
        builder.Property(d => d.StorageKey).HasMaxLength(300).IsRequired();
        builder.Property(d => d.OriginalFileName).HasMaxLength(255).IsRequired();
        builder.Property(d => d.ContentType).HasMaxLength(100).IsRequired();
        builder.Property(d => d.RejectionReason).HasMaxLength(500);

        builder.HasOne<ProviderProfile>().WithMany().HasForeignKey(d => d.ProviderId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(d => d.ReviewedBy).OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(d => d.ProviderId).HasDatabaseName("ix_provider_documents_provider_id");
    }
}