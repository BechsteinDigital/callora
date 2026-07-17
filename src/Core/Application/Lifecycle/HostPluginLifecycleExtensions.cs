using Callora.Core.Application.Plugins.Contracts;

namespace Callora.Core.Application.Lifecycle;

/// <summary>
/// Convenience helpers over the host plugin lifecycle facade.
/// </summary>
public static class HostPluginLifecycleExtensions
{
    /// <summary>
    /// Finds one plugin descriptor by id, ignoring case.
    /// </summary>
    public static HostPluginDescriptor? FindDescriptor(this IHostPluginLifecycle lifecycle, string pluginId)
    {
        ArgumentNullException.ThrowIfNull(lifecycle);

        foreach (var plugin in lifecycle.Plugins)
        {
            if (string.Equals(plugin.PluginId, pluginId, StringComparison.OrdinalIgnoreCase))
            {
                return plugin;
            }
        }

        return null;
    }
}
