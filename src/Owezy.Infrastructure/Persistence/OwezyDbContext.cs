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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OwezyDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
