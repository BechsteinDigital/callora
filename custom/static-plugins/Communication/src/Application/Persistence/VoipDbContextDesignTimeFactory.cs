using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Callora.Plugin.Communication.Application.Persistence;

/// <summary>
/// Design-time factory so `dotnet ef migrations add` can build the context
/// without a running host. Runtime binding to the host database is done by
/// the host's IPluginDbContextProvider (PLAT-260).
/// </summary>
public sealed class VoipDbContextDesignTimeFactory : IDesignTimeDbContextFactory<VoipDbContext>
{
    public VoipDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<VoipDbContext>()
            .UseNpgsql(
                "Host=localhost;Database=callora_design;Username=callora;Password=callora",
                npgsql => npgsql.MigrationsAssembly(typeof(VoipDbContext).Assembly.GetName().Name))
            .Options;
        return new VoipDbContext(options);
    }
}
