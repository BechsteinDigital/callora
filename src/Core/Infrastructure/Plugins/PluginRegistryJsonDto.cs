namespace Callora.Core.Infrastructure.Plugins;

public sealed class PluginRegistryJsonDto
{
    public string? ContractVersion { get; set; }

    public string? SchemaVersion { get; set; }

    public string? Name { get; set; }

    public string? PluginId { get; set; }

    public string? Version { get; set; }

    public string? AssemblyFileName { get; set; }

    public string? EntryTypeName { get; set; }

    /// <summary>Deployment tier: "system" (foundation) or "application" (default).</summary>
    public string? Tier { get; set; }

    public string[]? Capabilities { get; set; }

    public string[]? ConditionalCapabilities { get; set; }

    public string[]? RequiresCapabilities { get; set; }

    public Dictionary<string, string>? Dependencies { get; set; }

    public PluginRegistryExtensionJsonDto[]? Extensions { get; set; }
}
