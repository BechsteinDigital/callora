using Callora.Core.Application.Configuration;
using System.Text.Json;

namespace Callora.Core.Infrastructure.Configuration;

/// <summary>
/// Reads the optional "config.fields" schema from a plugin's registry.json
/// (located next to or above the plugin assembly) and syncs it into the
/// system config definitions — the registry.json counterpart to Shopware's
/// config.xml.
/// </summary>
public sealed class RegistryConfigSchemaSyncService(ISystemConfigStore store)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

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

        var definitions = ParseConfigFields(await File.ReadAllTextAsync(registryPath, cancellationToken).ConfigureAwait(false));
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

    public static IReadOnlyList<SystemConfigDefinitionInput> ParseConfigFields(string registryJson)
    {
        using var document = JsonDocument.Parse(registryJson, new JsonDocumentOptions { AllowTrailingCommas = true });
        if (!document.RootElement.TryGetProperty("config", out var config) ||
            config.ValueKind != JsonValueKind.Object ||
            !config.TryGetProperty("fields", out var fields) ||
            fields.ValueKind != JsonValueKind.Object)
        {
            return [];
        }

        var definitions = new List<SystemConfigDefinitionInput>();
        var sortOrder = 0;
        foreach (var field in fields.EnumerateObject())
        {
            sortOrder += 10;
            var value = field.Value;
            definitions.Add(new SystemConfigDefinitionInput(
                ConfigKey: field.Name.Trim(),
                Label: ReadString(value, "label") ?? field.Name,
                FieldType: ReadString(value, "type") ?? "text",
                Description: ReadString(value, "description") ?? ReadString(value, "helpText"),
                DefaultValueJson: value.TryGetProperty("default", out var defaultValue)
                    ? defaultValue.GetRawText()
                    : value.TryGetProperty("value", out var legacyValue) ? legacyValue.GetRawText() : null,
                GroupName: ReadString(value, "group") ?? ReadString(value, "tab"),
                OptionsJson: value.TryGetProperty("options", out var options) ? options.GetRawText() : null,
                SortOrder: value.TryGetProperty("order", out var order) && order.TryGetInt32(out var orderValue)
                    ? orderValue
                    : sortOrder,
                IsActive: !value.TryGetProperty("disabled", out var disabled) || !disabled.GetBoolean()));
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
