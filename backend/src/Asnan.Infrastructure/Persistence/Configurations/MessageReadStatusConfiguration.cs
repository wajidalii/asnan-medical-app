using Asnan.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Asnan.Infrastructure.Persistence.Configurations;

public class MessageReadStatusConfiguration : IEntityTypeConfiguration<MessageReadStatus>
{
    public void Configure(EntityTypeBuilder<MessageReadStatus> builder)
    {
        builder.ToTable("MessageReadStatuses");

        builder.HasKey(r => r.Id);

        builder.HasOne(r => r.ChatConversation)
            .WithMany()
            .HasForeignKey(r => r.ChatConversationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(r => r.User)
            .WithMany()
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // One read-tracking row per (conversation, participant) — the upsert target for MarkAsReadAsync.
        builder.HasIndex(r => new { r.ChatConversationId, r.UserId }).IsUnique();
    }
}
