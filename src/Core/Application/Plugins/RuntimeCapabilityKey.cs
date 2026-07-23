namespace Callora.Core.Application.Plugins;

/// <summary>
/// Identity of one tracked runtime capability inside <see cref="RuntimeCapabilityRegistry"/>:
/// plugin + capability + scope, normalized to invariant lowercase so lookups are case-insensitive
/// (plugin ids and capability codes are case-insensitive; workspace keys are the tenant axis).
/// </summary>
internal readonly record struct RuntimeCapabilityKey(string PluginId, string Capability, string? WorkspaceKey)
{
    public static RuntimeCapabilityKey Create(string pluginId, string capability, string? workspaceKey) =>
        new(
            pluginId.ToLowerInvariant(),
            capability.ToLowerInvariant(),
            workspaceKey?.ToLowerInvariant());
}
