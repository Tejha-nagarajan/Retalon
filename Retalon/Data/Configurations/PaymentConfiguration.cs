using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Retalon.Models.Entities;

namespace Retalon.Data.Configurations;

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        // Primary Key
        builder.HasKey(p => p.PaymentId);


        // Stripe Payment Intent ID
        builder.Property(p => p.StripePaymentIntentId)
            .IsRequired()
            .HasMaxLength(255);

        builder.HasIndex(p => p.StripePaymentIntentId)
            .IsUnique();


        // Money precision
        builder.Property(p => p.Amount)
            .HasPrecision(18, 2);


        // Currency
        builder.Property(p => p.Currency)
            .IsRequired()
            .HasMaxLength(10);


        // Payment Status Enum
        builder.Property(p => p.PaymentStatus)
            .HasConversion<string>()
            .HasMaxLength(50);


        // Failure reason
        builder.Property(p => p.FailureReason)
            .HasMaxLength(1000);


        // Default date
        builder.Property(p => p.CreatedDate)
            .HasDefaultValueSql("GETUTCDATE()");
    }
}