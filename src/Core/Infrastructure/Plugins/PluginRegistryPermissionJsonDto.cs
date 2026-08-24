namespace Callora.Core.Infrastructure.Plugins;

/// <summary>One entry of the manifest's <c>permissions</c> array.</summary>
public sealed class PluginRegistryPermissionJsonDto
{
    /// <summary>The permission key, inside the plugin's own namespace.</summary>
    public string? Key { get; set; }

    /// <summary>What granting it allows, shown to the operator granting it.</summary>
    public string? Description { get; set; }
}
