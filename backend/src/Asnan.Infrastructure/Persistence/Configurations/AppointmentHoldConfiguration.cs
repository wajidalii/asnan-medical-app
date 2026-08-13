using Asnan.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Asnan.Infrastructure.Persistence.Configurations;

public class AppointmentHoldConfiguration : IEntityTypeConfiguration<AppointmentHold>
{
    public void Configure(EntityTypeBuilder<AppointmentHold> builder)
    {
        builder.ToTable("AppointmentHolds");

        builder.HasKey(h => h.Id);

        builder.Property(h => h.Status).HasConversion<int>();
        builder.Property(h => h.HoldTokenHash).HasMaxLength(512).IsRequired();

        builder.HasOne(h => h.DoctorProfile)
            .WithMany()
            .HasForeignKey(h => h.DoctorProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(h => h.PatientUser)
            .WithMany()
            .HasForeignKey(h => h.PatientUserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(h => h.HoldTokenHash).IsUnique();

        // See AppointmentHold.ActiveSlotKey's doc comment: this is an
        // application-maintained "NULL is distinct" filtered-unique-index
        // trick (same idea as Users.Email/Mobile), not a DB-computed column
        // — a unique index over it is what makes a concurrent second insert
        // for the same doctor+slot fail at the DB level. Expiry isn't
        // encoded here; stale Active holds are flipped to Expired (and this
        // column nulled out) lazily by AppointmentHoldService.
        builder.Property(h => h.ActiveSlotKey).HasMaxLength(128);
        builder.HasIndex(h => h.ActiveSlotKey).IsUnique();
    }
}
