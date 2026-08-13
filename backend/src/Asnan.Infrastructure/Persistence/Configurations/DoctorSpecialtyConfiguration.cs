using Asnan.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Asnan.Infrastructure.Persistence.Configurations;

public class DoctorSpecialtyConfiguration : IEntityTypeConfiguration<DoctorSpecialty>
{
    public void Configure(EntityTypeBuilder<DoctorSpecialty> builder)
    {
        builder.ToTable("DoctorSpecialties");

        builder.HasKey(ds => new { ds.DoctorProfileId, ds.SpecialtyId });

        builder.HasOne(ds => ds.DoctorProfile)
            .WithMany(d => d.DoctorSpecialties)
            .HasForeignKey(ds => ds.DoctorProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(ds => ds.Specialty)
            .WithMany(s => s.DoctorSpecialties)
            .HasForeignKey(ds => ds.SpecialtyId)
            .OnDelete(DeleteBehavior.Restrict);

        // Must match DoctorProfile's soft-delete filter — see UserRoleConfiguration for why.
        builder.HasQueryFilter(ds => ds.DoctorProfile.DeletedAtUtc == null && ds.DoctorProfile.User.DeletedAtUtc == null);
    }
}
