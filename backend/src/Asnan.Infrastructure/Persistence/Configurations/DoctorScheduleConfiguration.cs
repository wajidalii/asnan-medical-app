using Asnan.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Asnan.Infrastructure.Persistence.Configurations;

public class DoctorScheduleConfiguration : IEntityTypeConfiguration<DoctorSchedule>
{
    public void Configure(EntityTypeBuilder<DoctorSchedule> builder)
    {
        builder.ToTable("DoctorSchedules");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.DayOfWeek).HasConversion<int>();

        builder.HasOne(s => s.DoctorProfile)
            .WithMany()
            .HasForeignKey(s => s.DoctorProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(s => new { s.DoctorProfileId, s.DayOfWeek });

        // Must match DoctorProfile's soft-delete filter — see UserRoleConfiguration for why.
        builder.HasQueryFilter(s => s.DeletedAtUtc == null && s.DoctorProfile.DeletedAtUtc == null && s.DoctorProfile.User.DeletedAtUtc == null);
    }
}
