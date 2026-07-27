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
    void ConfigureOptions(DbContextOptionsBuilder builder, Assembly migrationsAssembly);

    /// <summary>Advisory lock key derived from the plugin id.</summary>
    long GetMigrationLockKey(string pluginId);
}
