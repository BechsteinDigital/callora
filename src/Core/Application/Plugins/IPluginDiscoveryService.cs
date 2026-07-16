namespace Callora.Core.Application.Plugins;

/// <summary>
/// Reconciles the local plugin directories against the installation registry
/// (Shopware <c>plugin:refresh</c> equivalent): newly-present plugins are recorded,
/// changed manifests are updated, and plugins whose assembly disappeared are removed
/// when inactive or reported when active. Callable on demand (admin overview, CLI)
/// and at startup — never a file-system watcher. Installation and activation remain
/// deliberate, separate operator actions.
/// </summary>
public interface IPluginDiscoveryService
{
    /// <summary>Scans the local plugin directories and reconciles the registry.</summary>
    Task<PluginDiscoveryRefreshResult> RefreshAsync(CancellationToken cancellationToken = default);
}

/// <summary>Outcome of one refresh, per plugin id.</summary>
/// <param name="Added">Newly discovered plugins recorded into the registry.</param>
/// <param name="Updated">Existing plugins whose manifest changed and were updated.</param>
/// <param name="RemovedInactive">Inactive plugins whose assembly disappeared and were uninstalled.</param>
/// <param name="MissingActive">Active plugins whose assembly disappeared — kept and reported, not removed.</param>
public sealed record PluginDiscoveryRefreshResult(
    IReadOnlyList<string> Added,
    IReadOnlyList<string> Updated,
    IReadOnlyList<string> RemovedInactive,
    IReadOnlyList<string> MissingActive)
{
    /// <summary>An empty result (nothing changed).</summary>
    public static PluginDiscoveryRefreshResult Empty { get; } = new([], [], [], []);
}
