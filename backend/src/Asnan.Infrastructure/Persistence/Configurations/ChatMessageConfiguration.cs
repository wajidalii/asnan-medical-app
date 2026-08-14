using Asnan.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Asnan.Infrastructure.Persistence.Configurations;

public class ChatMessageConfiguration : IEntityTypeConfiguration<ChatMessage>
{
    public void Configure(EntityTypeBuilder<ChatMessage> builder)
    {
        builder.ToTable("ChatMessages");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Content).HasMaxLength(2000).IsRequired();

        builder.HasOne(m => m.ChatConversation)
            .WithMany()
            .HasForeignKey(m => m.ChatConversationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(m => m.SenderUser)
            .WithMany()
            .HasForeignKey(m => m.SenderUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Cursor pagination (issue #28) orders/filters by (conversation, sent time).
        builder.HasIndex(m => new { m.ChatConversationId, m.SentAtUtc });
    }
}
