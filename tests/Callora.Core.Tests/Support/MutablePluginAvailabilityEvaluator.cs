using Callora.Core.Application.Plugins;

namespace Callora.Core.Tests.Support;

/// <summary>
/// Availability test double whose verdict can change between calls, so a test can
/// model a lapse and its restoration against one running processor —
/// <see cref="StaticPluginAvailabilityEvaluator"/> is fixed for its lifetime.
/// </summary>
public sealed class MutablePluginAvailabilityEvaluator(params string[] unavailablePluginIds)
    : IPluginAvailabilityEvaluator
{
    private readonly HashSet<string> _unavailable =
        new(unavailablePluginIds, StringComparer.OrdinalIgnoreCase);

    public void RestoreAll() => _unavailable.Clear();

    public Task<PluginAvailability> EvaluateAsync(
        string pluginId,
        string workspaceKey,
        CancellationToken cancellationToken = default)
    {
        var available = !_unavailable.Contains(pluginId);
        IReadOnlyList<PluginAvailabilityFactor> unmet = available
            ? Array.Empty<PluginAvailabilityFactor>()
            : [PluginAvailabilityFactor.Entitled];
        return Task.FromResult(new PluginAvailability(available, unmet));
    }
}
