using Asnan.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Asnan.Infrastructure.Persistence.Configurations;

public class SignupTokenConfiguration : IEntityTypeConfiguration<SignupToken>
{
    public void Configure(EntityTypeBuilder<SignupToken> builder)
    {
        builder.ToTable("SignupTokens");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Destination).HasMaxLength(256).IsRequired();
        builder.Property(t => t.TokenHash).HasMaxLength(512).IsRequired();

        builder.HasIndex(t => t.TokenHash).IsUnique();
    }
}
