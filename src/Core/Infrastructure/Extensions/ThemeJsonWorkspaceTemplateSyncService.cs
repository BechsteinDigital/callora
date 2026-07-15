using System.Text.Json;
using Callora.Core.Application.Extensions;

namespace Callora.Core.Infrastructure.Extensions;

public sealed class ThemeJsonWorkspaceTemplateSyncService(
    IWorkspaceTemplateRegistryStore store,
    IWorkspaceThemeSettingsStore settingsStore,
    ILogger<ThemeJsonWorkspaceTemplateSyncService> logger) : IThemeJsonWorkspaceTemplateSyncService
{
    public async Task SyncFromAssemblyAsync(
        string pluginId,
        string version,
        string assemblyPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        ArgumentException.ThrowIfNullOrWhiteSpace(assemblyPath);

        var readResult = await TryReadDefinitionsFromThemeJsonAsync(assemblyPath, cancellationToken).ConfigureAwait(false);
        if (!readResult.ShouldReplaceDefinitions)
        {
            await SyncThemeSettingsFromThemeJsonAsync(pluginId, version, assemblyPath, cancellationToken).ConfigureAwait(false);
            return;
        }

        await store.ReplaceDefinitionsForPluginAsync(pluginId, version, readResult.Definitions, cancellationToken).ConfigureAwait(false);
        await SyncThemeSettingsFromThemeJsonAsync(pluginId, version, assemblyPath, cancellationToken).ConfigureAwait(false);
    }

    public Task ClearPluginDefinitionsAsync(
        string pluginId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        return ClearPluginDefinitionsCoreAsync(pluginId, cancellationToken);
    }

    private async Task ClearPluginDefinitionsCoreAsync(
        string pluginId,
        CancellationToken cancellationToken)
    {
        var existingDefinitions = await store
            .ListDefinitionsAsync(surface: null, isActive: null, cancellationToken)
            .ConfigureAwait(false);

        var versions = existingDefinitions
            .Where(x => string.Equals(x.PluginId, pluginId, StringComparison.OrdinalIgnoreCase))
            .Select(x => x.Version)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (var version in versions)
        {
            await store
                .ReplaceDefinitionsForPluginAsync(pluginId, version, Array.Empty<WorkspaceTemplateDefinitionInput>(), cancellationToken)
                .ConfigureAwait(false);
        }

        await settingsStore.ClearPluginDefinitionsAsync(pluginId, cancellationToken).ConfigureAwait(false);
    }

    private async Task SyncThemeSettingsFromThemeJsonAsync(
        string pluginId,
        string version,
        string assemblyPath,
        CancellationToken cancellationToken)
    {
        var settingsResult = await ReadSettingsFromThemeJsonAsync(assemblyPath, cancellationToken).ConfigureAwait(false);
        if (!settingsResult.ShouldReplaceDefinitions)
        {
            return;
        }

        await settingsStore
            .ReplaceDefinitionsForPluginAsync(pluginId, version, settingsResult.Definitions, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<ThemeJsonDefinitionReadResult> TryReadDefinitionsFromThemeJsonAsync(
        string assemblyPath,
        CancellationToken cancellationToken)
    {
        var fullAssemblyPath = Path.GetFullPath(assemblyPath);
        var directory = Path.GetDirectoryName(fullAssemblyPath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            return new ThemeJsonDefinitionReadResult(false, Array.Empty<WorkspaceTemplateDefinitionInput>());
        }

        var themeJsonPath = Path.Combine(directory, "theme.json");
        if (!File.Exists(themeJsonPath))
        {
            logger.LogInformation("No theme.json found for plugin assembly {AssemblyPath}.", fullAssemblyPath);
            return new ThemeJsonDefinitionReadResult(false, Array.Empty<WorkspaceTemplateDefinitionInput>());
        }

        try
        {
            var json = await File.ReadAllTextAsync(themeJsonPath, cancellationToken).ConfigureAwait(false);
            using var document = JsonDocument.Parse(json);
            return ParseDefinitions(document.RootElement);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Invalid theme.json found at {ThemeJsonPath}.", themeJsonPath);
            return new ThemeJsonDefinitionReadResult(false, Array.Empty<WorkspaceTemplateDefinitionInput>());
        }
    }

    private static ThemeJsonDefinitionReadResult ParseDefinitions(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return new ThemeJsonDefinitionReadResult(false, Array.Empty<WorkspaceTemplateDefinitionInput>());
        }

        var rootSurface = TryGetString(root, "surface");
        var container = TryGetArray(root, "definitions") ?? TryGetArray(root, "templates");
        if (container is null)
        {
            return new ThemeJsonDefinitionReadResult(false, Array.Empty<WorkspaceTemplateDefinitionInput>());
        }

        var definitions = new List<WorkspaceTemplateDefinitionInput>();
        foreach (var item in container.Value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var templateKey = FirstNonEmpty(
                TryGetString(item, "templateKey"),
                TryGetString(item, "key"),
                TryGetString(item, "id"));
            if (string.IsNullOrWhiteSpace(templateKey))
            {
                continue;
            }

            var displayName = FirstNonEmpty(
                TryGetString(item, "displayName"),
                TryGetString(item, "name"),
                TryGetString(item, "label"),
                templateKey);
            var templatePath = FirstNonEmpty(
                TryGetString(item, "templatePath"),
                TryGetString(item, "path"),
                TryGetString(item, "template"),
                $"views/workspace/{templateKey}.html");
            var parentTemplateKey = FirstNonEmptyOrNull(
                TryGetString(item, "parentTemplateKey"),
                TryGetString(item, "extends"));
            var surface = FirstNonEmpty(
                TryGetString(item, "surface"),
                rootSurface,
                "workspace");
            var scope = FirstNonEmpty(TryGetString(item, "scope"), "workspace");
            var isActive = TryGetBoolean(item, "isActive") ?? TryGetBoolean(item, "active") ?? true;
            var priority = TryGetInt32(item, "priority") ?? TryGetInt32(item, "order") ?? 100;

            definitions.Add(new WorkspaceTemplateDefinitionInput(
                TemplateKey: templateKey.Trim(),
                Surface: surface.Trim(),
                DisplayName: displayName.Trim(),
                TemplatePath: templatePath.Trim(),
                ParentTemplateKey: parentTemplateKey?.Trim(),
                Scope: scope.Trim(),
                IsActive: isActive,
                Priority: priority));
        }

        return new ThemeJsonDefinitionReadResult(true, definitions);
    }

    private async Task<ThemeJsonSettingsReadResult> ReadSettingsFromThemeJsonAsync(
        string assemblyPath,
        CancellationToken cancellationToken)
    {
        var fullAssemblyPath = Path.GetFullPath(assemblyPath);
        var directory = Path.GetDirectoryName(fullAssemblyPath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            return new ThemeJsonSettingsReadResult(false, Array.Empty<WorkspaceThemeSettingDefinitionInput>());
        }

        var themeJsonPath = Path.Combine(directory, "theme.json");
        if (!File.Exists(themeJsonPath))
        {
            return new ThemeJsonSettingsReadResult(false, Array.Empty<WorkspaceThemeSettingDefinitionInput>());
        }

        try
        {
            var json = await File.ReadAllTextAsync(themeJsonPath, cancellationToken).ConfigureAwait(false);
            using var document = JsonDocument.Parse(json);
            return ParseSettings(document.RootElement);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Invalid theme.json found at {ThemeJsonPath}.", themeJsonPath);
            return new ThemeJsonSettingsReadResult(false, Array.Empty<WorkspaceThemeSettingDefinitionInput>());
        }
    }

    private static ThemeJsonSettingsReadResult ParseSettings(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return new ThemeJsonSettingsReadResult(false, Array.Empty<WorkspaceThemeSettingDefinitionInput>());
        }

        if (!root.TryGetProperty("config", out var configElement) || configElement.ValueKind != JsonValueKind.Object)
        {
            return new ThemeJsonSettingsReadResult(false, Array.Empty<WorkspaceThemeSettingDefinitionInput>());
        }

        if (!configElement.TryGetProperty("fields", out var fieldsElement) || fieldsElement.ValueKind != JsonValueKind.Object)
        {
            return new ThemeJsonSettingsReadResult(true, Array.Empty<WorkspaceThemeSettingDefinitionInput>());
        }

        var definitions = new List<WorkspaceThemeSettingDefinitionInput>();
        var index = 0;
        foreach (var property in fieldsElement.EnumerateObject())
        {
            if (property.Value.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var field = property.Value;
            var label = FirstNonEmpty(
                TryGetString(field, "label"),
                TryGetString(field, "name"),
                property.Name);
            var fieldType = FirstNonEmpty(
                TryGetString(field, "type"),
                "text");
            var description = FirstNonEmptyOrNull(
                TryGetString(field, "helpText"),
                TryGetString(field, "help"),
                TryGetString(field, "description"));
            var defaultValueJson = TryGetRawJson(field, "value") ?? TryGetRawJson(field, "defaultValue");
            var required = TryGetBoolean(field, "required") ?? false;
            var groupName = FirstNonEmptyOrNull(
                TryGetString(field, "tab"),
                TryGetString(field, "group"),
                TryGetString(field, "section"));
            var optionsJson = TryGetRawJson(field, "options");
            var isActive = !(TryGetBoolean(field, "disabled") ?? false);
            var sortOrder = TryGetInt32(field, "order") ?? TryGetInt32(field, "position") ?? ((index + 1) * 10);

            definitions.Add(new WorkspaceThemeSettingDefinitionInput(
                SettingKey: property.Name.Trim(),
                Label: label.Trim(),
                FieldType: fieldType.Trim().ToLowerInvariant(),
                Description: description,
                DefaultValueJson: defaultValueJson,
                IsRequired: required,
                SortOrder: sortOrder,
                GroupName: groupName,
                OptionsJson: optionsJson,
                IsActive: isActive));
            index++;
        }

        return new ThemeJsonSettingsReadResult(true, definitions);
    }

    private static JsonElement? TryGetArray(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        return value;
    }

    private static string? TryGetString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return value.GetString();
    }

    private static bool? TryGetBoolean(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null
        };
    }

    private static int? TryGetInt32(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private static string? TryGetRawJson(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        return value.GetRawText();
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return string.Empty;
    }

    private static string? FirstNonEmptyOrNull(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return null;
    }

}
