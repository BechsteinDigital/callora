namespace Callora.Host.PluginContracts.Application.Migrations;

/// <summary>
/// Host service applying pending plugin migrations exactly once, with
/// bookkeeping per plugin and version. Call this first in StartAsync.
/// </summary>
public interface IPluginMigrationRunner
{
    Task RunAsync(
        string pluginId,
        IReadOnlyList<IPluginMigration> migrations,
        CancellationToken cancellationToken = default);
}
