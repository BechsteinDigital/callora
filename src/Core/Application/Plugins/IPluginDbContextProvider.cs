using System.Reflection;
using Microsoft.EntityFrameworkCore;

namespace Callora.Core.Application.Plugins;

/// <summary>
/// Host-supplied EF Core configuration for plugin databases (PLAT-260):
/// binds a plugin DbContext to the shared host database and its migrations
/// assembly. The concrete provider (Npgsql, connection string) lives in the
/// backend so this hosting layer stays provider-agnostic.
/// </summary>
public interface IPluginDbContextProvider
{
    /// <summary>
    /// Configures the options builder with the host database connection and
    /// the plugin's migrations assembly. The assembly is passed as a loaded
    /// <see cref="Assembly"/> instance — not by name — so EF Core never issues
    /// an <c>Assembly.Load</c> from its own (host) load context, which cannot
    /// resolve a plugin assembly that lives in the plugin's collectible ALC.
    /// </summary>
    /// <param name="pluginId">
    /// Wem der Kontext gehört. Nicht Zierde: EF Core legt sein gebautes Modell in einem internen
    /// Dienstanbieter ab, den es prozessweit zwischenspeichert und nach dem Kontext-TYP schlüsselt.
    /// Dieser Typ gehört dem Plugin — ein geteilter Speicher hält damit dessen Ladekontext fest,
    /// solange der Prozess läuft. Wer die Optionen baut, muss deshalb wissen, für wen.
    /// </param>
    void ConfigureOptions(DbContextOptionsBuilder builder, Assembly migrationsAssembly, string pluginId);

    /// <summary>Advisory lock key derived from the plugin id.</summary>
    long GetMigrationLockKey(string pluginId);

    /// <summary>
    /// Gibt frei, was für dieses Plugin gehalten wurde. Nach dem Anhalten, vor dem Entladen.
    /// </summary>
    /// <remarks>
    /// Ohne das bliebe der interne EF-Dienstanbieter mit dem gebauten Modell stehen — und damit die
    /// Entitätstypen des Plugins und sein Ladekontext. Ein Plugin, das je einen DbContext gebaut hat,
    /// ließ sich vorher nie wieder entladen; die Aktualisierung meldete „still pinned after unload".
    /// </remarks>
    void ReleasePlugin(string pluginId);
}
