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

        // Makes the "mark this token used" update in RefreshTokenService atomic:
        // if two concurrent refresh calls both read UsedAtUtc == null for the same
        // token, only the first SaveChangesAsync succeeds — the second throws
        // DbUpdateConcurrencyException (its WHERE clause's original-value check on
        // UsedAtUtc no longer matches), which the service treats as reuse-detected
        // rather than letting both requests silently rotate the same token.
        builder.Property(t => t.UsedAtUtc).IsConcurrencyToken();
    }
}
