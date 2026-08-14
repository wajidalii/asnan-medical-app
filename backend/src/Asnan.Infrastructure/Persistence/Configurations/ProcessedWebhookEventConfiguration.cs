using Asnan.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Asnan.Infrastructure.Persistence.Configurations;

public class ProcessedWebhookEventConfiguration : IEntityTypeConfiguration<ProcessedWebhookEvent>
{
    public void Configure(EntityTypeBuilder<ProcessedWebhookEvent> builder)
    {
        builder.ToTable("ProcessedWebhookEvents");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.ProviderEventId).HasMaxLength(255).IsRequired();

        builder.HasIndex(e => e.ProviderEventId).IsUnique();
    }
}
