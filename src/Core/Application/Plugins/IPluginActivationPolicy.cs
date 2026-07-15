using Callora.Core.Application.Plugins;

namespace Callora.Core.Application.Plugins;

public interface IPluginActivationPolicy
{
    ValueTask<PluginActivationDecision> EvaluateAsync(
        string pluginId,
        string? tenantId = null,
        CancellationToken cancellationToken = default);
}
