using System.Data.Common;

namespace Callora.Host.PluginContracts.Application.Migrations;

/// <summary>
/// One schema migration owned by a plugin. Plugins create their own tables —
/// by convention named "plugin_&lt;pluginId&gt;_*" — and run pending migrations
/// as the FIRST step of StartAsync via <see cref="IPluginMigrationRunner"/>.
/// </summary>
public interface IPluginMigration
{
    /// <summary>Monotonically increasing version, e.g. 1, 2, 3.</summary>
    int Version { get; }

    string Description { get; }

    /// <summary>
    /// Applies the migration. Commands MUST enlist in the provided
    /// transaction so schema change and bookkeeping commit atomically.
    /// </summary>
    Task UpAsync(
        DbConnection connection,
        DbTransaction transaction,
        CancellationToken cancellationToken = default);
}
