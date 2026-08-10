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

    /// <summary>
    /// Der Advisory-Lock-Schlüssel, unter dem die Migrationen dieses Plugins serialisiert werden.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Hier stand <c>StringComparer.Ordinal.GetHashCode(pluginId)</c>. Der String-Hash ist in
    /// .NET seit Core je PROZESS randomisiert: Zwei Host-Instanzen berechneten für dieselbe
    /// Plugin-Id verschiedene Schlüssel, nahmen verschiedene Locks und migrierten dasselbe
    /// Schema gleichzeitig. Nachgemessen für <c>"comm"</c> in zwei Prozessen: -1859540138 gegen
    /// -1761266879.
    /// </para>
    /// <para>
    /// Ein Lock, der bei jedem Start woanders hinzeigt, ist schlimmer als keiner: Er gibt die
    /// Zusage von Serialisierung, ohne sie zu halten, und bricht erst beim gleichzeitigen Start
    /// zweier Instanzen — dem Fall, für den er da ist.
    /// </para>
    /// <para>
    /// SHA-256 statt eines schnelleren Hashes, weil er stabil spezifiziert ist; die Kosten
    /// fallen einmal je Plugin und Migrationslauf an. Kleingeschrieben, weil Plugin-Ids überall
    /// ohne Rücksicht auf Groß-/Kleinschreibung verglichen werden — ein Lock, der "Comm" und
    /// "comm" trennte, hätte dieselbe Wirkung wie gar keiner.
    /// </para>
    /// </remarks>
    public long GetMigrationLockKey(string pluginId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        var normalized = pluginId.Trim().ToLowerInvariant();
        var digest = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(normalized));
        var hash = (long)System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(digest);
        return (PluginLockNamespace << 32) | hash;
    }
}
