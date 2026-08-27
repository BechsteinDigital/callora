using System.Text.Json.Serialization;

namespace Callora.Host.Cli.Application;

/// <summary>One declared permission key, as the manifest carries it.</summary>
internal sealed class PluginInspectionPermission
{
    [JsonPropertyName("key")]
    public string? Key { get; set; }
}
