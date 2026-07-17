using Callora.Core.Application.Webhooks;
using System.Text.Json;

namespace Callora.Core.Infrastructure.Webhooks;

/// <summary>
/// Reads the optional "sensitiveFields" array from a plugin's registry.json —
/// the payload field names that plugin's events carry as person-related data —
/// and syncs them into the <see cref="SensitivePayloadFieldRegistry"/> so webhook
/// data-minimization masks them, without the core hardcoding any domain field.
/// </summary>
public sealed class RegistrySensitiveFieldSyncService(SensitivePayloadFieldRegistry registry)
{
    public async Task SyncFromAssemblyAsync(
        string pluginId,
        string assemblyPath,
        CancellationToken cancellationToken = default)
    {
        var registryPath = FindRegistryPath(assemblyPath);
        if (registryPath is null)
        {
            registry.ClearPluginFields(pluginId);
            return;
        }

        var fields = ParseSensitiveFields(
            await File.ReadAllTextAsync(registryPath, cancellationToken).ConfigureAwait(false));
        registry.RegisterPluginFields(pluginId, fields);
    }

    public void ClearPlugin(string pluginId) => registry.ClearPluginFields(pluginId);

    public static IReadOnlyList<string> ParseSensitiveFields(string registryJson)
    {
        using var document = JsonDocument.Parse(registryJson, new JsonDocumentOptions { AllowTrailingCommas = true });
        if (!document.RootElement.TryGetProperty("sensitiveFields", out var fields) ||
            fields.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var result = new List<string>();
        foreach (var field in fields.EnumerateArray())
        {
            if (field.ValueKind == JsonValueKind.String && field.GetString() is { Length: > 0 } name)
            {
                result.Add(name.Trim());
            }
        }

        return result;
    }

    private static string? FindRegistryPath(string assemblyPath)
    {
        if (string.IsNullOrWhiteSpace(assemblyPath))
        {
            return null;
        }

        var current = new DirectoryInfo(Path.GetDirectoryName(Path.GetFullPath(assemblyPath)) ?? string.Empty);
        // Bounded walk: the registry sits at the plugin root (bin/Debug/net10.0
        // is three levels below); never crawl toward the filesystem root.
        for (var depth = 0; current is not null && depth < 4; depth++)
        {
            var candidate = Path.Combine(current.FullName, "registry.json");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        return null;
    }
}
