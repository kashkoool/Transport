using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TransportPlatform.Domain.Notifications;

namespace TransportPlatform.Infrastructure.Persistence.Configurations;

internal sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("notification");
        builder.HasKey(n => n.Id);
        builder.Property(n => n.Title).HasMaxLength(200).IsRequired();
        builder.Property(n => n.Message).HasMaxLength(2000).IsRequired();
        builder.Property(n => n.Type).HasMaxLength(20).IsRequired();

        // Unread-count query: WHERE RecipientUserId = ? AND IsRead = false.
        builder.HasIndex(n => new { n.RecipientUserId, n.IsRead });
        // Inbox list query: WHERE RecipientUserId = ? ORDER BY CreatedAtUtc DESC.
        builder.HasIndex(n => new { n.RecipientUserId, n.CreatedAtUtc });
        // No FK to AspNetUsers (RecipientUserId is a soft reference, like refresh_token.UserId).
    }
}
