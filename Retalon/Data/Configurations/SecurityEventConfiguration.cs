using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Retalon.Models.Entities;

namespace Retalon.Data.Configurations;

public class SecurityEventConfiguration : IEntityTypeConfiguration<SecurityEvent>
{
    public void Configure(EntityTypeBuilder<SecurityEvent> builder)
    {
        // Primary Key
        builder.HasKey(s => s.SecurityEventId);


        // Security Event Type Enum
        builder.Property(s => s.SecurityEventType)
            .HasConversion<string>()
            .HasMaxLength(50);


        // Description
        builder.Property(s => s.Description)
            .IsRequired()
            .HasMaxLength(1000);


        // IP Address
        builder.Property(s => s.IpAddress)
            .HasMaxLength(50);


        // Default Created Date
        builder.Property(s => s.CreatedDate)
            .HasDefaultValueSql("GETUTCDATE()");


        // Optional User relationship
        builder.HasOne(s => s.User)
            .WithMany()
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.SetNull);


        // Useful indexes
        builder.HasIndex(s => s.UserId);

        builder.HasIndex(s => s.SecurityEventType);

        builder.HasIndex(s => s.CreatedDate);
    }
}