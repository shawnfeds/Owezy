using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Owezy.Infrastructure.Persistence;

internal sealed class BillItemSharerConfiguration : IEntityTypeConfiguration<BillItemSharerRow>
{
    public void Configure(EntityTypeBuilder<BillItemSharerRow> builder)
    {
        builder.ToTable("BillItemSharers");

        builder.HasKey(s => new { s.ItemId, s.ParticipantId });

        builder.HasOne(s => s.Item)
            .WithMany(i => i.Sharers)
            .HasForeignKey(s => s.ItemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(s => s.Participant)
            .WithMany()
            .HasForeignKey(s => s.ParticipantId)
            .OnDelete(DeleteBehavior.NoAction); // Avoid multiple cascade paths in SQL Server
    }
}
