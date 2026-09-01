using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Retalon.Models.Entities;

namespace Retalon.Data.Configurations;

public class SearchHistoryConfiguration : IEntityTypeConfiguration<SearchHistory>
{
    public void Configure(EntityTypeBuilder<SearchHistory> builder)
    {
        // Primary Key
        builder.HasKey(s => s.SearchHistoryId);


        // Search Term
        builder.Property(s => s.SearchTerm)
            .IsRequired()
            .HasMaxLength(500);


        // Default Search Date
        builder.Property(s => s.SearchDate)
            .HasDefaultValueSql("GETUTCDATE()");


        // User (1) -> Search Histories (Many)
        builder.HasOne(s => s.User)
            .WithMany()
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);


        // Useful indexes
        builder.HasIndex(s => s.UserId);

        builder.HasIndex(s => s.SearchDate);
    }
}