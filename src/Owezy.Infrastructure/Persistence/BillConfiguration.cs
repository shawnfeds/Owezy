using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Owezy.Infrastructure.Persistence;

internal sealed class BillConfiguration : IEntityTypeConfiguration<BillRow>
{
    public void Configure(EntityTypeBuilder<BillRow> builder)
    {
        builder.ToTable("Bills");

        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).ValueGeneratedNever();

        builder.Property(b => b.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(b => b.SplitterPhoneNumber)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(b => b.Status)
            .IsRequired();

        builder.Property(b => b.CreatedAt)
            .IsRequired();

        builder.Property(b => b.FinalizedAt)
            .IsRequired(false);


        builder.HasMany(b => b.Participants)
            .WithOne(p => p.Bill)
            .HasForeignKey(p => p.BillId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
