using Asnan.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Asnan.Infrastructure.Persistence.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.EventType).HasMaxLength(64).IsRequired();
        builder.Property(a => a.Detail).HasMaxLength(1024);
        builder.Property(a => a.IpAddress).HasMaxLength(64);

        builder.HasIndex(a => new { a.UserId, a.OccurredAtUtc });
    }
}
