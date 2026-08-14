using Asnan.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Asnan.Infrastructure.Persistence.Configurations;

public class AppointmentConfiguration : IEntityTypeConfiguration<Appointment>
{
    public void Configure(EntityTypeBuilder<Appointment> builder)
    {
        builder.ToTable("Appointments");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Status).HasConversion<int>();
        builder.Property(a => a.ConsultationFee).HasColumnType("decimal(10,2)");
        builder.Property(a => a.Currency).HasMaxLength(3).IsRequired();

        builder.HasOne(a => a.DoctorProfile)
            .WithMany()
            .HasForeignKey(a => a.DoctorProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.PatientUser)
            .WithMany()
            .HasForeignKey(a => a.PatientUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(a => a.SourceHoldId);
        builder.HasIndex(a => new { a.PatientUserId, a.Status });
        builder.HasIndex(a => new { a.DoctorProfileId, a.SlotStartUtc });
    }
}
