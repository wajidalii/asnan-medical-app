using Asnan.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Asnan.Infrastructure.Persistence.Configurations;

public class DoctorProfileConfiguration : IEntityTypeConfiguration<DoctorProfile>
{
    public void Configure(EntityTypeBuilder<DoctorProfile> builder)
    {
        builder.ToTable("DoctorProfiles");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.FullName).HasMaxLength(256).IsRequired();
        builder.Property(d => d.Bio).HasMaxLength(2000);
        builder.Property(d => d.ConsultationFee).HasColumnType("decimal(10,2)");
        builder.Property(d => d.Currency).HasMaxLength(3).IsRequired();
        builder.Property(d => d.TimeZoneId).HasMaxLength(64).IsRequired();
        builder.Property(d => d.ClinicAddress).HasMaxLength(512);

        builder.HasIndex(d => d.UserId).IsUnique();

        builder.HasOne(d => d.User)
            .WithMany()
            .HasForeignKey(d => d.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Must match Users' soft-delete filter — see UserRoleConfiguration for why.
        builder.HasQueryFilter(d => d.DeletedAtUtc == null && d.User.DeletedAtUtc == null);
    }
}
