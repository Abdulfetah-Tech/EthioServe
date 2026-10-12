using EthioServe.Domain.Entities;
using EthioServe.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EthioServe.Infrastructure.Persistence.Configurations;

public sealed class AppointmentConfiguration : IEntityTypeConfiguration<Appointment>
{
    public void Configure(EntityTypeBuilder<Appointment> builder)
    {
        builder.ToTable("appointments", t =>
        {
            t.HasCheckConstraint("ck_appointments_time_range", "end_at > start_at");
            t.HasCheckConstraint("ck_appointments_status", ConfigurationExtensions.InList<AppointmentStatus>("status"));
        });
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Address).HasMaxLength(300).IsRequired();
        builder.Property(a => a.Notes).HasMaxLength(1000);
        builder.Property(a => a.Status).AsStringColumn(20);
        builder.Property(a => a.Version).UseXminVersion();

        builder.HasOne<ServiceRequest>().WithMany().HasForeignKey(a => a.ServiceRequestId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Quote>().WithMany().HasForeignKey(a => a.QuoteId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ProviderProfile>().WithMany().HasForeignKey(a => a.ProviderId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CustomerProfile>().WithMany().HasForeignKey(a => a.CustomerId).OnDelete(DeleteBehavior.Restrict);

               builder.HasIndex(a => a.ServiceRequestId, "ux_appointments_one_active_per_request")
            .HasDatabaseName("ux_appointments_one_active_per_request")
            .IsUnique().HasFilter("status NOT IN ('Cancelled', 'NoShow')");
            
        builder.HasIndex(a => new { a.ProviderId, a.StartAt }).HasDatabaseName("ix_appointments_provider_start");
        builder.HasIndex(a => new { a.CustomerId, a.StartAt }).HasDatabaseName("ix_appointments_customer_start");
    }
}