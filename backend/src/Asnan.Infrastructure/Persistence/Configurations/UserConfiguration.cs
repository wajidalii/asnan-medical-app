using Asnan.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Asnan.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.Email).HasMaxLength(256);
        builder.Property(u => u.Mobile).HasMaxLength(32);
        builder.Property(u => u.PasswordHash).HasMaxLength(512);

        // MySQL/MariaDB unique indexes treat multiple NULLs as distinct,
        // so this enforces "unique when present" without a filtered index.
        builder.HasIndex(u => u.Email).IsUnique();
        builder.HasIndex(u => u.Mobile).IsUnique();

        builder.HasQueryFilter(u => u.DeletedAtUtc == null);
    }
}
