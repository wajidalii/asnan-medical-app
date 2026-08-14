using Asnan.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Asnan.Infrastructure.Persistence.Configurations;

public class PatientProfileConfiguration : IEntityTypeConfiguration<PatientProfile>
{
    public void Configure(EntityTypeBuilder<PatientProfile> builder)
    {
        builder.ToTable("PatientProfiles");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.FullName).HasMaxLength(200).IsRequired();
        builder.Property(p => p.Gender).HasConversion<int?>();
        builder.Property(p => p.Phone).HasMaxLength(30);
        builder.Property(p => p.AddressLine).HasMaxLength(500);
        builder.Property(p => p.EmergencyContactName).HasMaxLength(200);
        builder.Property(p => p.EmergencyContactPhone).HasMaxLength(30);

        // One profile per user.
        builder.HasIndex(p => p.UserId).IsUnique();

        builder.HasOne(p => p.User)
            .WithMany()
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Same pattern as DoctorProfileConfiguration — a soft-deleted user's profile stops surfacing everywhere, not just at login (UserConfiguration's own filter).
        builder.HasQueryFilter(p => p.DeletedAtUtc == null && p.User.DeletedAtUtc == null);
    }
}
