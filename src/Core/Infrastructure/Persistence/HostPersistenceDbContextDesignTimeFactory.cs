using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Callora.Core.Infrastructure.Persistence;

/// <summary>
/// Design-time factory for EF Core tooling (dotnet ef migrations). Uses a
/// placeholder connection string; migrations are generated offline.
/// </summary>
public sealed class HostPersistenceDbContextDesignTimeFactory : IDesignTimeDbContextFactory<HostPersistenceDbContext>
{
    public HostPersistenceDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<HostPersistenceDbContext>()
            .UseNpgsql("Host=localhost;Database=callora_design;Username=callora;Password=callora")
            .Options;

        return new HostPersistenceDbContext(options);
    }
}
