using Asnan.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Asnan.Infrastructure.Persistence.Configurations;

public class SpecialtyConfiguration : IEntityTypeConfiguration<Specialty>
{
    public void Configure(EntityTypeBuilder<Specialty> builder)
    {
        builder.ToTable("Specialties");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Name).HasMaxLength(128).IsRequired();
        builder.Property(s => s.Description).HasMaxLength(1024);

        builder.HasIndex(s => s.Name).IsUnique();

        builder.HasQueryFilter(s => s.DeletedAtUtc == null);
    }
}
