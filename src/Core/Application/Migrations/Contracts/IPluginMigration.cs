using System.Data.Common;
using Callora.Core.Extensibility;

namespace Callora.Core.Application.Migrations.Contracts;

/// <summary>
/// One schema migration owned by a plugin. Plugins create their own tables —
/// by convention named "plugin_&lt;pluginId&gt;_*" — and run pending migrations
/// as the FIRST step of StartAsync via <see cref="IPluginMigrationRunner"/>.
/// </summary>
[CalloraExtensible("Extension point — implement to define a plugin schema migration (REV2 §8.2)")]
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
