using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Retalon.Models.Entities;

namespace Retalon.Data.Configurations;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        // Primary Key
        builder.HasKey(rt => rt.RefreshTokenId);

        // Token
        builder.Property(rt => rt.TokenHash)
            .IsRequired()
            .HasMaxLength(500);

        // Make token unique
        builder.HasIndex(rt => rt.TokenHash)
            .IsUnique();

        // Relationship
        builder.HasOne(rt => rt.User)
            .WithMany(u => u.RefreshTokens)
            .HasForeignKey(rt => rt.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Default value
        builder.Property(rt => rt.CreatedDate)
            .HasDefaultValueSql("GETUTCDATE()");
    }
}