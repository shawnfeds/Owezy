using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Owezy.Infrastructure.Persistence;

internal sealed class ParticipantAccessLinkConfiguration : IEntityTypeConfiguration<ParticipantAccessLinkRow>
{
    public void Configure(EntityTypeBuilder<ParticipantAccessLinkRow> builder)
    {
        builder.ToTable("ParticipantAccessLinks");

        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).ValueGeneratedNever();

        builder.Property(l => l.BillId)
            .IsRequired();

        builder.Property(l => l.ParticipantId)
            .IsRequired();

        builder.Property(l => l.TokenHash)
            .IsRequired()
            .HasMaxLength(128);

        builder.HasIndex(l => l.TokenHash)
            .IsUnique();

        builder.Property(l => l.CreatedAt)
            .IsRequired();

        builder.Property(l => l.IsRevoked)
            .IsRequired();
    }
}
