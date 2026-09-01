using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Retalon.Models.Entities;

namespace Retalon.Data.Configurations;

public class ProcurementConfiguration : IEntityTypeConfiguration<Procurement>
{
    public void Configure(EntityTypeBuilder<Procurement> builder)
    {
        // Primary Key
        builder.HasKey(p => p.ProcurementId);


        // Required Quantity
        builder.Property(p => p.RequiredQuantity)
            .IsRequired();


        // Procurement Status Enum
        builder.Property(p => p.ProcurementStatus)
            .HasConversion<string>()
            .HasMaxLength(50);


        // Default Created Date
        builder.Property(p => p.CreatedDate)
            .HasDefaultValueSql("GETUTCDATE()");


        // Order (1) -> Procurements (Many)
        builder.HasOne(p => p.Order)
            .WithMany(o => o.Procurements)
            .HasForeignKey(p => p.OrderId)
            .OnDelete(DeleteBehavior.Restrict);


        // Product (1) -> Procurements (Many)
        builder.HasOne(p => p.Product)
            .WithMany()
            .HasForeignKey(p => p.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}