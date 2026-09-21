using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Retalon.Entities;

namespace Retalon.Configurations
{
    public class ImportBatchItemConfiguration : IEntityTypeConfiguration<ImportBatchItem>
    {
        public void Configure(EntityTypeBuilder<ImportBatchItem> builder)
        {
            builder.HasKey(x => x.ImportBatchItemId);

            builder.Property(x => x.Status)
                .IsRequired()
                .HasMaxLength(50);

            builder.Property(x => x.Error)
                .HasMaxLength(4000);

            builder.HasIndex(x => new
            {
                x.ImportBatchId,
                x.RowNumber
            })
            .IsUnique();

            builder.HasOne(x => x.ImportBatch)
                .WithMany()
                .HasForeignKey(x => x.ImportBatchId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(x => x.Product)
                .WithMany()
                .HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}