using System.Text.Json.Serialization;

namespace Callora.Host.Cli.Application;

/// <summary>
/// The manifest fields worth reporting. Wider than
/// <see cref="PluginRegistryManifest"/>, which carries only what contract testing validates.
/// </summary>
internal sealed class PluginInspectionManifest
{
    [JsonPropertyName("contractVersion")]
    public string? ContractVersion { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("pluginId")]
    public string? PluginId { get; set; }

    [JsonPropertyName("version")]
    public string? Version { get; set; }

    [JsonPropertyName("entryTypeName")]
    public string? EntryTypeName { get; set; }

    [JsonPropertyName("capabilities")]
    public string[]? Capabilities { get; set; }

    [JsonPropertyName("requiresCapabilities")]
    public string[]? RequiresCapabilities { get; set; }

    [JsonPropertyName("dependencies")]
    public Dictionary<string, string>? Dependencies { get; set; }

    [JsonPropertyName("permissions")]
    public PluginInspectionPermission[]? Permissions { get; set; }
}
