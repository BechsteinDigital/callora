using Callora.Core.Application.Plugins;

namespace Callora.Core.Tests.Support;

/// <summary>
/// Test double for plugin availability: every plugin is available except the
/// ids passed as unavailable. Lets UI/serving tests model a lapsed entitlement
/// or missing capability without wiring the six real availability stores.
/// </summary>
public sealed class StaticPluginAvailabilityEvaluator(params string[] unavailablePluginIds)
    : IPluginAvailabilityEvaluator
{
    private readonly HashSet<string> _unavailable =
        new(unavailablePluginIds, StringComparer.OrdinalIgnoreCase);

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
