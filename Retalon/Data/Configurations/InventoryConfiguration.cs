using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Retalon.Models.Entities;

namespace Retalon.Data.Configurations;

public class InventoryConfiguration : IEntityTypeConfiguration<Inventory>
{
    public void Configure(EntityTypeBuilder<Inventory> builder)
    {
        // Primary Key
        builder.HasKey(i => i.InventoryId);


        // ProductId must be unique because:
        // One Product = One Inventory
        builder.HasIndex(i => i.ProductId)
            .IsUnique();


        // Quantity fields
        builder.Property(i => i.QuantityAvailable)
            .HasDefaultValue(0);

        builder.Property(i => i.QuantityReserved)
            .HasDefaultValue(0);

        builder.Property(i => i.SafetyStockLevel)
            .HasDefaultValue(0);

        builder.Property(i => i.ProcurementLeadTimeDays)
            .HasDefaultValue(0);


        // Last Updated
        builder.Property(i => i.LastUpdated)
            .HasDefaultValueSql("GETUTCDATE()");
    }
}