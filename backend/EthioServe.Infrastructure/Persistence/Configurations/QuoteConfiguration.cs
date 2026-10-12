using EthioServe.Domain.Entities;
using EthioServe.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EthioServe.Infrastructure.Persistence.Configurations;

public sealed class QuoteConfiguration : IEntityTypeConfiguration<Quote>
{
    public void Configure(EntityTypeBuilder<Quote> builder)
    {
        builder.ToTable("quotes", t =>
        {
            t.HasCheckConstraint("ck_quotes_amount", "amount > 0");
            t.HasCheckConstraint("ck_quotes_duration", "estimated_duration_minutes > 0");
            t.HasCheckConstraint("ck_quotes_status", ConfigurationExtensions.InList<QuoteStatus>("status"));
        });
        builder.HasKey(q => q.Id);

        builder.Property(q => q.Amount).HasPrecision(12, 2);
        builder.Property(q => q.Description).HasMaxLength(2000).IsRequired();
        builder.Property(q => q.Status).AsStringColumn(20);
        builder.Property(q => q.Version).UseXminVersion();

        builder.HasOne<ServiceRequest>().WithMany().HasForeignKey(q => q.ServiceRequestId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<ProviderProfile>().WithMany().HasForeignKey(q => q.ProviderId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(q => q.ServiceRequestId).HasDatabaseName("ix_quotes_service_request_id");

        // The database itself guarantees a request can never have two accepted quotes,
        // and a provider can only have one open quote per request (even under concurrent requests).
        builder.HasIndex(q => q.ServiceRequestId, "ux_quotes_one_accepted_per_request")
            .IsUnique().HasFilter("status = 'Accepted'");
        builder.HasIndex(q => new { q.ServiceRequestId, q.ProviderId }, "ux_quotes_one_pending_per_provider")
            .IsUnique().HasFilter("status = 'Pending'");

        builder.HasIndex(q => new { q.ProviderId, q.Status }).HasDatabaseName("ix_quotes_provider_status");
        builder.HasIndex(q => new { q.Status, q.ValidUntil }).HasDatabaseName("ix_quotes_status_valid_until");
    }
}