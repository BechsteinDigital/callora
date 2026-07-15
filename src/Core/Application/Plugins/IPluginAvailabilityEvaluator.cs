namespace Callora.Core.Application.Plugins;

/// <summary>
/// Derives whether a plugin is effectively available in a workspace (REV2 §3.2).
/// Serving paths depend on this abstraction so the single canonical derivation
/// (<see cref="PluginAvailability.From"/>) is reused at runtime, never
/// re-implemented per consumer.
/// </summary>
public interface IPluginAvailabilityEvaluator
{
    Task<PluginAvailability> EvaluateAsync(
        string pluginId,
        string workspaceKey,
        CancellationToken cancellationToken = default);
}
