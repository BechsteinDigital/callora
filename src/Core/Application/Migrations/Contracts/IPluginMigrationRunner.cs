namespace Callora.Core.Application.Migrations.Contracts;

/// <summary>
/// Host service applying pending plugin migrations exactly once, with
/// bookkeeping per plugin and version. Call this first in StartAsync.
/// </summary>
public interface IPluginMigrationRunner
{
    /// <summary>
    /// Applies every migration not yet recorded for the plugin, in version order,
    /// each in its own transaction with bookkeeping committed atomically.
    /// </summary>
    Task RunAsync(
        string pluginId,
        IReadOnlyList<IPluginMigration> migrations,
        CancellationToken cancellationToken = default);
}
