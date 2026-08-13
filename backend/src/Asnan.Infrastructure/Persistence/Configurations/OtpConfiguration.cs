using Asnan.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Asnan.Infrastructure.Persistence.Configurations;

public class OtpConfiguration : IEntityTypeConfiguration<Otp>
{
    public void Configure(EntityTypeBuilder<Otp> builder)
    {
        builder.ToTable("Otps");

        builder.HasKey(o => o.Id);

        builder.Property(o => o.Destination).HasMaxLength(256).IsRequired();
        builder.Property(o => o.CodeHash).HasMaxLength(256).IsRequired();

        // Lookup path for "find the active OTP for this destination + purpose".
        builder.HasIndex(o => new { o.Destination, o.Purpose });
    }
}
