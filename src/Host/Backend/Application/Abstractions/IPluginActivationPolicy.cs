namespace Callora.Host.Backend.Application.Abstractions;

public interface IPluginActivationPolicy
{
    ValueTask<PluginActivationDecision> EvaluateAsync(
        string pluginId,
        string? tenantId = null,
        CancellationToken cancellationToken = default);
}

public readonly record struct PluginActivationDecision(bool IsAllowed, string? Reason)
{
    public static PluginActivationDecision Allow() => new(true, null);

    public static PluginActivationDecision Deny(string reason) => new(false, reason);
}
