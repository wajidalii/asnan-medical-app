using Asnan.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Asnan.Infrastructure.Persistence.Configurations;

public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("Roles");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.Name).HasMaxLength(32).IsRequired();
        builder.HasIndex(r => r.Name).IsUnique();

        builder.HasData(
            new Role { Id = RoleIds.Patient, Name = "Patient" },
            new Role { Id = RoleIds.Doctor, Name = "Doctor" },
            new Role { Id = RoleIds.Admin, Name = "Admin" });
    }
}
