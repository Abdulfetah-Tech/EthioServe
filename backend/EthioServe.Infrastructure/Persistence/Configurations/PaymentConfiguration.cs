using EthioServe.Domain.Entities;
using EthioServe.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EthioServe.Infrastructure.Persistence.Configurations;

public sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("payments", t =>
        {
            t.HasCheckConstraint("ck_payments_amount", "amount > 0");
            t.HasCheckConstraint("ck_payments_status", ConfigurationExtensions.InList<PaymentStatus>("status"));
            t.HasCheckConstraint("ck_payments_method", ConfigurationExtensions.InList<PaymentMethod>("payment_method"));
        });
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Amount).HasPrecision(12, 2);
        builder.Property(p => p.Currency).HasMaxLength(3).IsFixedLength().IsRequired();
        builder.Property(p => p.PaymentMethod).AsStringColumn(20);
        builder.Property(p => p.Status).AsStringColumn(20);
        builder.Property(p => p.TransactionReference).HasMaxLength(100).IsRequired();
        builder.Property(p => p.IdempotencyKey).HasMaxLength(100);
        builder.Property(p => p.FailureReason).HasMaxLength(300);
        builder.Property(p => p.Version).UseXminVersion();

        builder.HasOne<ServiceRequest>().WithMany().HasForeignKey(p => p.ServiceRequestId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Quote>().WithMany().HasForeignKey(p => p.QuoteId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CustomerProfile>().WithMany().HasForeignKey(p => p.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ProviderProfile>().WithMany().HasForeignKey(p => p.ProviderId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(p => p.TransactionReference).IsUnique().HasDatabaseName("ux_payments_transaction_reference");
        builder.HasIndex(p => p.IdempotencyKey).IsUnique()
            .HasFilter("idempotency_key IS NOT NULL").HasDatabaseName("ux_payments_idempotency_key");
        builder.HasIndex(p => p.ServiceRequestId).HasDatabaseName("ix_payments_service_request_id");

        // A request can be paid only once (MVP rule enforced by the database).
        builder.HasIndex(p => p.ServiceRequestId, "ux_payments_one_completed_per_request")
            .IsUnique().HasFilter("status = 'Completed'");

        builder.HasIndex(p => new { p.ProviderId, p.Status }).HasDatabaseName("ix_payments_provider_status");
        builder.HasIndex(p => new { p.CustomerId, p.CreatedAt }).HasDatabaseName("ix_payments_customer_created");
    }
}