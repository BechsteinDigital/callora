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
    /// the given migrations assembly (the plugin assembly).
    /// </summary>
    void ConfigureOptions(DbContextOptionsBuilder builder, string migrationsAssemblyName);

    /// <summary>Advisory lock key derived from the plugin id.</summary>
    long GetMigrationLockKey(string pluginId);
}
