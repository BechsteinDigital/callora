using System.Collections.Concurrent;
using System.Reflection;
using Callora.Core.Application.Plugins;
using Callora.Core.Application.Policies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Callora.Core.Infrastructure.Persistence;

/// <summary>
/// Binds plugin DbContexts to the host Postgres database (PLAT-260): same
/// connection string, the plugin assembly as migrations assembly. Each
/// plugin keeps its tables in its own schema (set by the plugin context via
/// HasDefaultSchema), so uninstalling can drop that schema cleanly.
/// </summary>
public sealed class NpgsqlPluginDbContextProvider(
    BackendHostOptions options,
    // Optional: Ein Host ohne Aufzeichnung verhält sich unverändert. Hier ist die einzige
    // Stelle, an der ein PLUGIN-Kontext konfiguriert wird — ohne sie bliebe genau die Arbeit
    // unsichtbar, für die der Rekorder gebaut ist.
    Callora.Core.Infrastructure.Diagnostics.RecordingDbCommandInterceptor? recorder = null)
    : IPluginDbContextProvider, IDisposable
{
    // Distinct from the host migration lock so plugin and host migrations
    // never block each other on the same key.
    private const long PluginLockNamespace = 0x504C5547; // "PLUG"

    /// <summary>
    /// Je Plugin ein eigener interner EF-Dienstanbieter.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Der Grund ist der Modell-Cache.</b> Ohne <c>UseInternalServiceProvider</c> baut EF Core seinen
    /// internen Anbieter selbst und legt ihn in einem PROZESSWEITEN, statischen Zwischenspeicher ab.
    /// Darin liegt das gebaute Modell, geschlüsselt nach dem Kontext-Typ — und dieser Typ gehört dem
    /// Plugin. Damit hielt ein einziger Modellaufbau den Ladekontext des Plugins fest, bis der Prozess
    /// endete: Jede Aktualisierung meldete „still pinned after unload", jede Änderung brauchte einen
    /// Neustart.
    /// </para>
    /// <para>
    /// Verschärfend war, dass der Schlüssel dieses Zwischenspeichers die Optionen-Erweiterungen
    /// heranzieht, die Migrations-Assembly aber nicht: Alle Plugins teilten sich denselben Anbieter,
    /// und eine Tabelle hielt die Typen aller Ladekontexte.
    /// </para>
    /// <para>
    /// Je Plugin gebaut und beim Deaktivieren weggeworfen lebt der Speicher genau so lange wie das
    /// Plugin. Der Preis ist ein Anbieter je Plugin statt einem je Prozess — bei einer Handvoll Plugins
    /// ist das nichts gegen einen Ladekontext, der nie wieder verschwindet.
    /// </para>
    /// </remarks>
    private readonly ConcurrentDictionary<string, ServiceProvider> _internalServices =
        new(StringComparer.OrdinalIgnoreCase);

    public void ConfigureOptions(DbContextOptionsBuilder builder, Assembly migrationsAssembly, string pluginId)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(migrationsAssembly);
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);

        builder.UseInternalServiceProvider(
            _internalServices.GetOrAdd(
                pluginId.Trim(),
                static _ => new ServiceCollection().AddEntityFrameworkNpgsql().BuildServiceProvider()));

        builder.UseNpgsql(
            options.DatabaseConnectionString,
            // Pass the loaded assembly, not its name: EF Core's MigrationsAssembly
            // otherwise does Assembly.Load(name) from the host load context, which
            // cannot see the plugin assembly in its collectible ALC.
            npgsql => npgsql.MigrationsAssembly(migrationsAssembly));

        if (recorder is not null)
        {
            builder.AddInterceptors(recorder);
        }
    }

    /// <inheritdoc />
    public void ReleasePlugin(string pluginId)
    {
        if (!string.IsNullOrWhiteSpace(pluginId)
            && _internalServices.TryRemove(pluginId.Trim(), out var services))
        {
            services.Dispose();
        }
    }

    /// <summary>Gibt frei, was für alle Plugins gehalten wurde — beim Ende des Hosts.</summary>
    public void Dispose()
    {
        foreach (var pluginId in _internalServices.Keys)
        {
            ReleasePlugin(pluginId);
        }
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
