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
