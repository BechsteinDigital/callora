
using Callora.Host.Backend.Application.Plugins;

namespace Callora.Host.Backend.Tests.Support;

internal sealed class StaticPluginActivationPolicy(PluginActivationDecision decision) : IPluginActivationPolicy
{
    public ValueTask<PluginActivationDecision> EvaluateAsync(
        string pluginId,
        string? tenantId = null,
        CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(decision);
}
