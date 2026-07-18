using System.Text.Json;
using Callora.Core.Extensibility;

namespace Callora.Core.Application.Plugins.Signing;

/// <summary>
/// Serializes plugin signature manifests. The canonical form — deterministic field
/// order, files sorted by path, no whitespace, signature field excluded — is the
/// exact byte sequence that is signed and verified, so signing and verification
/// agree regardless of on-disk formatting.
/// </summary>
[CalloraInternal("Plugin signature serialization — not a plugin contract (REV2 §7.2)")]
public static class PluginSignatureManifestSerializer
{
    private static readonly JsonSerializerOptions FileJsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    /// <summary>The bytes that are signed/verified: the manifest without its signature.</summary>
    public static byte[] SerializeCanonical(PluginSignatureManifest manifest)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = false }))
        {
            writer.WriteStartObject();
            writer.WriteString("schemaVersion", manifest.SchemaVersion);
            writer.WriteString("pluginId", manifest.PluginId);
            writer.WriteString("version", manifest.Version);
            writer.WriteString("algorithm", manifest.Algorithm);
            writer.WriteString("signerFingerprint", manifest.SignerFingerprint);
            writer.WriteStartArray("files");
            foreach (var file in manifest.Files.OrderBy(static x => x.Path, StringComparer.Ordinal))
            {
                writer.WriteStartObject();
                writer.WriteString("path", file.Path);
                writer.WriteString("sha256", file.Sha256);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return buffer.ToArray();
    }

    /// <summary>The full on-disk JSON, including the signature.</summary>
    public static string SerializeToFileJson(PluginSignatureManifest manifest) =>
        JsonSerializer.Serialize(manifest, FileJsonOptions);

    public static PluginSignatureManifest? Deserialize(string json) =>
        JsonSerializer.Deserialize<PluginSignatureManifest>(json, FileJsonOptions);
}
