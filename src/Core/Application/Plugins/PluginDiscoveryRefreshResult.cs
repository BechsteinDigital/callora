namespace Callora.Core.Application.Plugins;

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
