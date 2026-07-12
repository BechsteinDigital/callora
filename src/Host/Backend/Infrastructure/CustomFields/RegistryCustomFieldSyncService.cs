using System.Text.Json;
using Callora.Host.Backend.Application.Abstractions.CustomFields;

namespace Callora.Host.Backend.Infrastructure.CustomFields;

/// <summary>
/// Reads the optional "customFields" section from a plugin's registry.json —
/// entity name → field key → { label, type, order } — and syncs it into the
/// custom field definitions.
/// </summary>
public sealed class RegistryCustomFieldSyncService(ICustomFieldStore store)
{
    private static readonly string[] AllowedEntityNames = ["workspace", "call", "user"];

    public async Task SyncFromAssemblyAsync(
        string pluginId,
        string version,
        string assemblyPath,
        CancellationToken cancellationToken = default)
    {
        var registryPath = FindRegistryPath(assemblyPath);
        if (registryPath is null)
        {
            return;
        }

        var definitions = ParseCustomFields(
            pluginId,
            version,
            await File.ReadAllTextAsync(registryPath, cancellationToken).ConfigureAwait(false));
        if (definitions.Count == 0)
        {
            return;
        }

        await store
            .ReplaceDefinitionsForPluginAsync(pluginId, version, definitions, cancellationToken)
            .ConfigureAwait(false);
    }

    public Task ClearPluginDefinitionsAsync(string pluginId, CancellationToken cancellationToken = default) =>
        store.ClearDefinitionsForPluginAsync(pluginId, cancellationToken);

    public static IReadOnlyList<CustomFieldDefinitionSnapshot> ParseCustomFields(
        string pluginId,
        string version,
        string registryJson)
    {
        using var document = JsonDocument.Parse(registryJson, new JsonDocumentOptions { AllowTrailingCommas = true });
        if (!document.RootElement.TryGetProperty("customFields", out var customFields) ||
            customFields.ValueKind != JsonValueKind.Object)
        {
            return [];
        }

        var definitions = new List<CustomFieldDefinitionSnapshot>();
        foreach (var entity in customFields.EnumerateObject())
        {
            var entityName = entity.Name.Trim().ToLowerInvariant();
            if (!AllowedEntityNames.Contains(entityName) || entity.Value.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var sortOrder = 0;
            foreach (var field in entity.Value.EnumerateObject())
            {
                sortOrder += 10;
                var value = field.Value;
                definitions.Add(new CustomFieldDefinitionSnapshot(
                    pluginId,
                    version,
                    entityName,
                    field.Name.Trim(),
                    ReadString(value, "label") ?? field.Name,
                    ReadString(value, "type") ?? "text",
                    value.TryGetProperty("order", out var order) && order.TryGetInt32(out var orderValue)
                        ? orderValue
                        : sortOrder,
                    IsActive: true));
            }
        }

        return definitions;
    }

    private static string? ReadString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static string? FindRegistryPath(string assemblyPath)
    {
        if (string.IsNullOrWhiteSpace(assemblyPath))
        {
            return null;
        }

        var current = new DirectoryInfo(Path.GetDirectoryName(Path.GetFullPath(assemblyPath)) ?? string.Empty);
        while (current is not null)
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
