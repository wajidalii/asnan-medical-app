using Asnan.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Asnan.Infrastructure.Persistence.Configurations;

public class PaymentTransactionConfiguration : IEntityTypeConfiguration<PaymentTransaction>
{
    public void Configure(EntityTypeBuilder<PaymentTransaction> builder)
    {
        builder.ToTable("PaymentTransactions");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Status).HasConversion<int>();
        builder.Property(t => t.Amount).HasColumnType("decimal(10,2)");
        builder.Property(t => t.Currency).HasMaxLength(3).IsRequired();
        builder.Property(t => t.ProviderSessionId).HasMaxLength(255).IsRequired();
        builder.Property(t => t.ProviderTransactionId).HasMaxLength(255);
        builder.Property(t => t.RedirectUrl).HasMaxLength(2048).IsRequired();
        builder.Property(t => t.IdempotencyKey).HasMaxLength(255).IsRequired();
        builder.Property(t => t.FailureReason).HasMaxLength(1000);

        builder.HasOne(t => t.Appointment)
            .WithMany()
            .HasForeignKey(t => t.AppointmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(t => t.IdempotencyKey).IsUnique();
        builder.HasIndex(t => t.AppointmentId);
    }
}
