using Asnan.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Asnan.Infrastructure.Persistence.Configurations;

public class DoctorAvailabilityExceptionConfiguration : IEntityTypeConfiguration<DoctorAvailabilityException>
{
    public void Configure(EntityTypeBuilder<DoctorAvailabilityException> builder)
    {
        builder.ToTable("DoctorAvailabilityExceptions");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Type).HasConversion<int>();
        builder.Property(e => e.Reason).HasMaxLength(512);

        builder.HasOne(e => e.DoctorProfile)
            .WithMany()
            .HasForeignKey(e => e.DoctorProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => new { e.DoctorProfileId, e.Date });

        // Must match DoctorProfile's soft-delete filter — see UserRoleConfiguration for why.
        builder.HasQueryFilter(e => e.DeletedAtUtc == null && e.DoctorProfile.DeletedAtUtc == null && e.DoctorProfile.User.DeletedAtUtc == null);
    }
}
