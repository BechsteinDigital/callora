namespace Callora.Host.Backend.Application.Abstractions;

public interface IPluginActivationPolicy
{
    ValueTask<PluginActivationDecision> EvaluateAsync(
        string pluginId,
        string? tenantId = null,
        CancellationToken cancellationToken = default);
}
