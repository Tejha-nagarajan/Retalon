using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Retalon.Models.Entities;

namespace Retalon.Data.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        // Primary Key
        builder.HasKey(a => a.AuditLogId);


        // Required fields
        builder.Property(a => a.Action)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(a => a.EntityName)
            .IsRequired()
            .HasMaxLength(100);


        // Optional fields
        builder.Property(a => a.IpAddress)
            .HasMaxLength(50);

        builder.Property(a => a.CorrelationId)
            .HasMaxLength(100);

        builder.Property(a => a.RequestId)
            .HasMaxLength(100);


        // Default timestamp
        builder.Property(a => a.Timestamp)
            .HasDefaultValueSql("GETUTCDATE()");


        // User relationship (optional)
        builder.HasOne(a => a.User)
            .WithMany()
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.SetNull);


        // Useful indexes for querying logs
        builder.HasIndex(a => a.UserId);

        builder.HasIndex(a => a.Timestamp);

        builder.HasIndex(a => a.EntityName);
    }
}