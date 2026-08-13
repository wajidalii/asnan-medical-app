using Asnan.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Asnan.Infrastructure.Persistence.Configurations;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.TokenHash).HasMaxLength(512).IsRequired();

        // The source of truth preventing a rotated token from being reused
        // undetected: a second row can never be inserted with the same hash.
        builder.HasIndex(t => t.TokenHash).IsUnique();

        builder.HasOne(t => t.UserSession)
            .WithMany(s => s.RefreshTokens)
            .HasForeignKey(t => t.UserSessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(t => t.UserSessionId);
    }
}
