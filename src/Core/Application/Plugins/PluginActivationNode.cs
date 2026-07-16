namespace Callora.Core.Application.Plugins;

/// <summary>One plugin's ordering-relevant metadata (REV2 §5.1).</summary>
/// <param name="PluginId">Stable plugin id.</param>
/// <param name="IsFoundation">True for System-tier (foundation) plugins, which are
/// preferred earlier when no capability edge forces an order.</param>
/// <param name="ProvidedCapabilities">Capabilities this plugin provides.</param>
/// <param name="RequiredCapabilities">Capabilities this plugin needs before it starts.</param>
internal sealed record PluginActivationNode(
    string PluginId,
    bool IsFoundation,
    IReadOnlyCollection<string> ProvidedCapabilities,
    IReadOnlyCollection<string> RequiredCapabilities);
