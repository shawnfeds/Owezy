using Microsoft.EntityFrameworkCore;

namespace Owezy.Infrastructure.Persistence;

/// <summary>
/// The EF Core DbContext for Owezy.
/// This type is Infrastructure-only and must not be referenced by Application or Domain layers.
/// </summary>
public sealed class OwezyDbContext : DbContext
{
    public OwezyDbContext(DbContextOptions<OwezyDbContext> options) : base(options)
    {
    }

    internal DbSet<OtpChallengeRow> OtpChallenges => Set<OtpChallengeRow>();
    internal DbSet<BillRow> Bills => Set<BillRow>();
    internal DbSet<BillParticipantRow> BillParticipants => Set<BillParticipantRow>();
    internal DbSet<BillItemRow> BillItems => Set<BillItemRow>();
    internal DbSet<BillItemSharerRow> BillItemSharers => Set<BillItemSharerRow>();
    internal DbSet<ParticipantAccessLinkRow> ParticipantAccessLinks => Set<ParticipantAccessLinkRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OwezyDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
