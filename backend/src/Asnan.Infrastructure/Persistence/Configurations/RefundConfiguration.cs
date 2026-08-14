using Asnan.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Asnan.Infrastructure.Persistence.Configurations;

public class RefundConfiguration : IEntityTypeConfiguration<Refund>
{
    public void Configure(EntityTypeBuilder<Refund> builder)
    {
        builder.ToTable("Refunds");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Status).HasConversion<int>();
        builder.Property(r => r.Amount).HasColumnType("decimal(10,2)");
        builder.Property(r => r.Currency).HasMaxLength(3).IsRequired();
        builder.Property(r => r.Reason).HasMaxLength(1000);
        builder.Property(r => r.ProviderRefundId).HasMaxLength(255);
        builder.Property(r => r.FailureReason).HasMaxLength(1000);

        builder.HasOne(r => r.Appointment)
            .WithMany()
            .HasForeignKey(r => r.AppointmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.PaymentTransaction)
            .WithMany()
            .HasForeignKey(r => r.PaymentTransactionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(r => r.AppointmentId);
        builder.HasIndex(r => r.PaymentTransactionId);
    }
}
