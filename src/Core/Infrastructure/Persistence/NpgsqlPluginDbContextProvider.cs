using Callora.Core.Application.Policies;
using Callora.Hosting.Application.Plugins;
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

    public void ConfigureOptions(DbContextOptionsBuilder builder, string migrationsAssemblyName)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.UseNpgsql(
            options.DatabaseConnectionString,
            npgsql => npgsql.MigrationsAssembly(migrationsAssemblyName));
    }

    public long GetMigrationLockKey(string pluginId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        var hash = (long)(uint)StringComparer.Ordinal.GetHashCode(pluginId.Trim());
        return (PluginLockNamespace << 32) | hash;
    }
}
