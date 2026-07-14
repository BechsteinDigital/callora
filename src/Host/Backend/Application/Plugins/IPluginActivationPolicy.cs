using Callora.Host.Backend.Application.Plugins;

namespace Callora.Host.Backend.Application.Plugins;

public interface IPluginActivationPolicy
{
    ValueTask<PluginActivationDecision> EvaluateAsync(
        string pluginId,
        string? tenantId = null,
        CancellationToken cancellationToken = default);
}
