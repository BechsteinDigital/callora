using Callora.Host.Backend.Application.Abstractions;

namespace Callora.Host.Backend.Application.Policies;

public sealed class AllowlistPluginActivationPolicy(BackendHostOptions options) : IPluginActivationPolicy
{
    private readonly HashSet<string> _allowlist = new(
        options.ActivationAllowlistPluginIds ?? [],
        StringComparer.OrdinalIgnoreCase);

    public ValueTask<PluginActivationDecision> EvaluateAsync(
        string pluginId,
        CancellationToken cancellationToken = default)
    {
        if (!options.RequireAllowlistForActivation)
            return ValueTask.FromResult(PluginActivationDecision.Allow());

        if (_allowlist.Contains(pluginId))
            return ValueTask.FromResult(PluginActivationDecision.Allow());

        return ValueTask.FromResult(
            PluginActivationDecision.Deny(
                $"Plugin '{pluginId}' is not present in activation allowlist."));
    }
}
