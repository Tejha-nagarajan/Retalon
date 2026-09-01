using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Retalon.Models.Entities;

namespace Retalon.Data.Configurations;

public class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        // Primary Key
        builder.HasKey(oi => oi.OrderItemId);


        // Quantity
        builder.Property(oi => oi.Quantity)
            .IsRequired();


        // Money precision
        builder.Property(oi => oi.UnitPrice)
            .HasPrecision(18, 2);


        // Delivery days
        builder.Property(oi => oi.DeliveryDays)
            .IsRequired();


        // Product (1) -> OrderItems (Many)
        builder.HasOne(oi => oi.Product)
            .WithMany()
            .HasForeignKey(oi => oi.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}