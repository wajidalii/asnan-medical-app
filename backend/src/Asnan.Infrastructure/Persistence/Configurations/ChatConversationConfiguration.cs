using Asnan.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Asnan.Infrastructure.Persistence.Configurations;

public class ChatConversationConfiguration : IEntityTypeConfiguration<ChatConversation>
{
    public void Configure(EntityTypeBuilder<ChatConversation> builder)
    {
        builder.ToTable("ChatConversations");

        builder.HasKey(c => c.Id);

        builder.HasOne(c => c.Appointment)
            .WithMany()
            .HasForeignKey(c => c.AppointmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(c => c.AppointmentId).IsUnique();
    }
}
