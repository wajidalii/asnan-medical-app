using Asnan.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Asnan.Infrastructure.Persistence.Configurations;

public class ReminderConfiguration : IEntityTypeConfiguration<Reminder>
{
    public void Configure(EntityTypeBuilder<Reminder> builder)
    {
        builder.ToTable("Reminders");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Status).HasConversion<int>();

        builder.HasOne(r => r.Appointment)
            .WithMany()
            .HasForeignKey(r => r.AppointmentId)
            .OnDelete(DeleteBehavior.Cascade);

        // The actual duplicate-prevention guarantee ("never sent twice for
        // the same appointment+offset") — see Reminder's doc comment.
        builder.HasIndex(r => new { r.AppointmentId, r.OffsetMinutes }).IsUnique();
    }
}
