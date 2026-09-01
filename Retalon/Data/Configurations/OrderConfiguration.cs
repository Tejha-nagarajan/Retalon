using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Retalon.Models.Entities;

namespace Retalon.Data.Configurations;

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        // Primary Key
        builder.HasKey(o => o.OrderId);


        // Money precision
        builder.Property(o => o.TotalAmount)
            .HasPrecision(18, 2);


        // Order Status Enum
        builder.Property(o => o.OrderStatus)
            .HasConversion<string>()
            .HasMaxLength(50);


        // Default date
        builder.Property(o => o.CreatedDate)
            .HasDefaultValueSql("GETUTCDATE()");


        // User (1) -> Orders (Many)
        builder.HasOne(o => o.User)
            .WithMany()
            .HasForeignKey(o => o.UserId)
            .OnDelete(DeleteBehavior.Restrict);


        // Order (1) -> OrderItems (Many)
        builder.HasMany(o => o.OrderItems)
            .WithOne(oi => oi.Order)
            .HasForeignKey(oi => oi.OrderId)
            .OnDelete(DeleteBehavior.Cascade);


        // Order (1) -> Payments (Many)
        builder.HasMany(o => o.Payments)
            .WithOne(p => p.Order)
            .HasForeignKey(p => p.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}