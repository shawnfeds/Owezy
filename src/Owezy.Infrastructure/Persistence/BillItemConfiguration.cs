using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Owezy.Infrastructure.Persistence;

internal sealed class BillItemConfiguration : IEntityTypeConfiguration<BillItemRow>
{
    public void Configure(EntityTypeBuilder<BillItemRow> builder)
    {
        builder.ToTable("BillItems");

        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).ValueGeneratedNever();

        builder.Property(i => i.BillId).IsRequired();

        builder.Property(i => i.Description)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(i => i.Quantity).IsRequired();

        builder.Property(i => i.Amount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.HasMany(i => i.Sharers)
            .WithOne(s => s.Item)
            .HasForeignKey(s => s.ItemId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
