using System.Text.Json;

namespace Callora.Hosting.Application.Plugins;

/// <summary>
/// Reads the "contracts" declaration from the registry.json next to a
/// plugin assembly. Deliberately minimal: full manifest validation lives
/// in the backend registry reader; the loader only needs the file list.
/// </summary>
internal static class PluginContractManifestReader
{
    public static IReadOnlyList<string> ReadDeclaredContracts(string pluginAssemblyPath)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(pluginAssemblyPath));
        if (directory is null)
        {
            return [];
        }

        var manifestPath = Path.Combine(directory, "registry.json");
        if (!File.Exists(manifestPath))
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                !document.RootElement.TryGetProperty("contracts", out var contracts) ||
                contracts.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            return contracts.EnumerateArray()
                .Where(static entry => entry.ValueKind == JsonValueKind.String)
                .Select(static entry => entry.GetString()!)
                .Where(static entry => !string.IsNullOrWhiteSpace(entry))
                .ToArray();
        }
        catch (JsonException)
        {
            // Ein defektes Manifest scheitert an der Backend-Validierung;
            // der Loader behandelt es wie "keine Verträge deklariert".
            return [];
        }
    }
}
