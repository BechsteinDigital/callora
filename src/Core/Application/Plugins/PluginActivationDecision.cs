using Callora.Core.Application.Plugins;

namespace Callora.Core.Application.Plugins;

public readonly record struct PluginActivationDecision(bool IsAllowed, string? Reason)
{
    public static PluginActivationDecision Allow() => new(true, null);

    public static PluginActivationDecision Deny(string reason) => new(false, reason);
}
