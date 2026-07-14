using Callora.Host.Backend.Application.Plugins;

namespace Callora.Host.Backend.Application.Plugins;

public readonly record struct PluginActivationDecision(bool IsAllowed, string? Reason)
{
    public static PluginActivationDecision Allow() => new(true, null);

    public static PluginActivationDecision Deny(string reason) => new(false, reason);
}
