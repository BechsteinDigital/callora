using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Callora.Plugin.Communication.Infrastructure.Persistence;

/// <summary>
/// Design-time factory so <c>dotnet ef migrations add</c> can build the context without a
/// running host. Runtime binding to the host database is done by the host's
/// <c>IPluginDbContextFactory</c> (PLAT-260); the connection string here is design-time only.
/// </summary>
public sealed class CommunicationDbContextDesignTimeFactory
    : IDesignTimeDbContextFactory<CommunicationDbContext>
{
    /// <inheritdoc />
    public CommunicationDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<CommunicationDbContext>()
            .UseNpgsql(
                "Host=localhost;Database=callora_design;Username=callora;Password=callora",
                npgsql => npgsql.MigrationsAssembly(typeof(CommunicationDbContext).Assembly.GetName().Name))
            .Options;

        return new CommunicationDbContext(options);
    }
}
