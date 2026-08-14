using Asnan.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Asnan.Infrastructure.Persistence.Configurations;

public class NotificationDeviceConfiguration : IEntityTypeConfiguration<NotificationDevice>
{
    public void Configure(EntityTypeBuilder<NotificationDevice> builder)
    {
        builder.ToTable("NotificationDevices");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.FcmToken).HasMaxLength(512).IsRequired();
        builder.Property(d => d.Platform).HasConversion<int>();

        // The actual "deduped by token" guarantee — see NotificationDevice's doc comment.
        builder.HasIndex(d => d.FcmToken).IsUnique();
        builder.HasIndex(d => d.UserId);

        builder.HasOne(d => d.User)
            .WithMany()
            .HasForeignKey(d => d.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
