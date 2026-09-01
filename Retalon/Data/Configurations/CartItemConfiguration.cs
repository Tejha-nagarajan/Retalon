using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Retalon.Models.Entities;

namespace Retalon.Data.Configurations;

public class CartItemConfiguration : IEntityTypeConfiguration<CartItem>
{
    public void Configure(EntityTypeBuilder<CartItem> builder)
    {
        // Primary Key
        builder.HasKey(ci => ci.CartItemId);

        // Quantity
        builder.Property(ci => ci.Quantity)
            .IsRequired();

        // Added Date
        builder.Property(ci => ci.AddedDate)
            .HasDefaultValueSql("GETUTCDATE()");

        // Product (1) -> CartItems (Many)
        builder.HasOne(ci => ci.Product)
            .WithMany()
            .HasForeignKey(ci => ci.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        // Prevent duplicate product entries in same cart
        builder.HasIndex(ci => new { ci.CartId, ci.ProductId })
            .IsUnique();
    }
}