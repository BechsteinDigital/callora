using System.Text.Json.Serialization;

namespace Callora.Host.Cli.Application;

internal sealed class PluginRegistryManifest
{
    [JsonPropertyName("contractVersion")]
    public string? ContractVersion { get; set; }

    [JsonPropertyName("schemaVersion")]
    public string? SchemaVersion { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("pluginId")]
    public string? PluginId { get; set; }

    [JsonPropertyName("version")]
    public string? Version { get; set; }

    [JsonPropertyName("assemblyFileName")]
    public string? AssemblyFileName { get; set; }

    [JsonPropertyName("entryTypeName")]
    public string? EntryTypeName { get; set; }
}
