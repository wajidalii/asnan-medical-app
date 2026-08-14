using Asnan.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Asnan.Infrastructure.Persistence.Configurations;

public class AppointmentStatusHistoryConfiguration : IEntityTypeConfiguration<AppointmentStatusHistory>
{
    public void Configure(EntityTypeBuilder<AppointmentStatusHistory> builder)
    {
        builder.ToTable("AppointmentStatusHistory");

        builder.HasKey(h => h.Id);

        builder.Property(h => h.FromStatus).HasConversion<int?>();
        builder.Property(h => h.ToStatus).HasConversion<int>();
        builder.Property(h => h.Reason).HasMaxLength(1000);

        builder.HasOne(h => h.Appointment)
            .WithMany(a => a.StatusHistory)
            .HasForeignKey(h => h.AppointmentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(h => h.AppointmentId);
    }
}
