using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Owezy.Infrastructure.Persistence;

internal sealed class BillParticipantConfiguration : IEntityTypeConfiguration<BillParticipantRow>
{
    public void Configure(EntityTypeBuilder<BillParticipantRow> builder)
    {
        builder.ToTable("BillParticipants");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.Property(p => p.BillId).IsRequired();

        builder.Property(p => p.PhoneNumber)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(p => p.JoinedAt).IsRequired();

        // Enforce uniqueness constraint at database level: duplicate participant membership in a single bill is prevented!
        builder.HasIndex(p => new { p.BillId, p.PhoneNumber })
            .IsUnique();
    }
}
