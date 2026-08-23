using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Owezy.Infrastructure.Persistence;

/// <summary>
/// Design-time factory used by 'dotnet ef migrations add' when targeting the Infrastructure project.
/// Not used at runtime — the production DbContext is registered via DI in the application startup.
/// </summary>
internal sealed class OwezyDbContextFactory : IDesignTimeDbContextFactory<OwezyDbContext>
{
    public OwezyDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<OwezyDbContext>()
            .UseSqlServer(
                "Server=(localdb)\\mssqllocaldb;Database=Owezy_Dev;Trusted_Connection=True;",
                sql => sql.MigrationsAssembly(typeof(OwezyDbContext).Assembly.FullName))
            .Options;

        return new OwezyDbContext(options);
    }
}
