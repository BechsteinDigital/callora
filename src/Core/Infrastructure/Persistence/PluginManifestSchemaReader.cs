using System.Text.Json;

namespace Callora.Core.Infrastructure.Persistence;

/// <summary>
/// Reads the optional "databaseSchema" field from a plugin's registry.json
/// next to its assembly (PLAT-260). Lets a plugin declare its EF schema
/// explicitly so the host cleans it up on uninstall instead of only
/// guessing plugin_&lt;id&gt;. Returns a sanitized schema name or null.
/// </summary>
public static class PluginManifestSchemaReader
{
    public static string? TryReadDatabaseSchema(string assemblyPath)
    {
        if (string.IsNullOrWhiteSpace(assemblyPath))
        {
            return null;
        }

        var directory = Path.GetDirectoryName(Path.GetFullPath(assemblyPath));
        if (directory is null)
        {
            return null;
        }

        var manifestPath = Path.Combine(directory, "registry.json");
        if (!File.Exists(manifestPath))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                !document.RootElement.TryGetProperty("databaseSchema", out var schema) ||
                schema.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            // Same identifier rules as the convention — the declared name is
            // used verbatim in DDL, so it must be a safe identifier.
            return PluginSchemaName.Sanitize(schema.GetString());
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
