using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Retalon.Models.Entities;

namespace Retalon.Data.Configurations;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        // Primary Key
        builder.HasKey(n => n.NotificationId);

        // Message
        builder.Property(n => n.Message)
            .IsRequired()
            .HasMaxLength(2000);

        // Enums
        builder.Property(n => n.NotificationType)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(n => n.NotificationStatus)
            .HasConversion<string>()
            .HasMaxLength(50);

        // Default Created Date
        builder.Property(n => n.CreatedDate)
            .HasDefaultValueSql("GETUTCDATE()");

        // User (1) -> Notifications (Many)
        builder.HasOne(n => n.User)
            .WithMany()
            .HasForeignKey(n => n.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}