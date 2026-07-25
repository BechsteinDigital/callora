using System.Reflection;
using Callora.Core.Application.Plugins;
using Callora.Core.Application.Policies;
using Microsoft.EntityFrameworkCore;

namespace Callora.Core.Infrastructure.Persistence;

/// <summary>
/// Binds plugin DbContexts to the host Postgres database (PLAT-260): same
/// connection string, the plugin assembly as migrations assembly. Each
/// plugin keeps its tables in its own schema (set by the plugin context via
/// HasDefaultSchema), so uninstalling can drop that schema cleanly.
/// </summary>
public sealed class NpgsqlPluginDbContextProvider(BackendHostOptions options) : IPluginDbContextProvider
{
    // Distinct from the host migration lock so plugin and host migrations
    // never block each other on the same key.
    private const long PluginLockNamespace = 0x504C5547; // "PLUG"

    public void ConfigureOptions(DbContextOptionsBuilder builder, Assembly migrationsAssembly)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(migrationsAssembly);
        builder.UseNpgsql(
            options.DatabaseConnectionString,
            // Pass the loaded assembly, not its name: EF Core's MigrationsAssembly
            // otherwise does Assembly.Load(name) from the host load context, which
            // cannot see the plugin assembly in its collectible ALC.
            npgsql => npgsql.MigrationsAssembly(migrationsAssembly));
    }

    public long GetMigrationLockKey(string pluginId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        var hash = (long)(uint)StringComparer.Ordinal.GetHashCode(pluginId.Trim());
        return (PluginLockNamespace << 32) | hash;
    }
}
