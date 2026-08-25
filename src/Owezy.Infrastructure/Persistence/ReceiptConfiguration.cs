using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Owezy.Infrastructure.Persistence;

internal sealed class ReceiptConfiguration : IEntityTypeConfiguration<ReceiptRow>
{
    public void Configure(EntityTypeBuilder<ReceiptRow> builder)
    {
        builder.ToTable("Receipts");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.BillId).IsRequired();
        builder.HasIndex(r => r.BillId);

        builder.Property(r => r.StorageKey)
            .IsRequired()
            .HasMaxLength(300);

        builder.Property(r => r.Status).IsRequired();

        builder.Property(r => r.CreatedAt).IsRequired();

        // OCR result stored as JSON string. No image binary stored in SQL.
        builder.Property(r => r.OcrResultJson)
            .IsRequired(false)
            .HasColumnType("nvarchar(max)");
    }
}
