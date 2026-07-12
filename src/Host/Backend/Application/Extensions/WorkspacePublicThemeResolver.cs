using System.Text.Json;
using Callora.Host.Backend.Application.Abstractions.Configuration;
using Callora.Host.Backend.Application.Abstractions.Extensions;
using Callora.Host.Backend.Application.Abstractions.Workspaces;

namespace Callora.Host.Backend.Application.Extensions;

/// <summary>
/// Resolves the effective theme setting values for one workspace: definition
/// defaults of the assigned theme plugin, overridden by workspace values.
/// </summary>
public sealed class WorkspacePublicThemeResolver(
    IWorkspaceManagementStore workspaceStore,
    IWorkspaceThemeSettingsStore settingsStore)
{
    public async Task<WorkspacePublicTheme?> ResolveAsync(
        string workspaceKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceKey);

        var workspace = await workspaceStore
            .GetAsync(workspaceKey.Trim(), cancellationToken)
            .ConfigureAwait(false);
        if (workspace is null ||
            !workspace.IsActive ||
            !workspace.TenantIsActive ||
            string.IsNullOrWhiteSpace(workspace.ThemePluginId) ||
            string.IsNullOrWhiteSpace(workspace.ThemeVersion))
        {
            return null;
        }

        var definitions = await settingsStore
            .ListDefinitionsAsync(workspace.ThemePluginId, workspace.ThemeVersion, cancellationToken)
            .ConfigureAwait(false);
        var overrides = await settingsStore
            .ListWorkspaceValuesAsync(workspace.WorkspaceKey, workspace.ThemePluginId, cancellationToken)
            .ConfigureAwait(false);

        var overridesByKey = overrides.ToDictionary(
            static value => value.SettingKey,
            static value => value.ValueJson,
            StringComparer.OrdinalIgnoreCase);

        // The endpoint is anonymous — secret-typed settings never leave here.
        var valuesByKey = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var definition in definitions.Where(static definition =>
                     definition.IsActive && !SystemConfigFieldTypes.IsSecret(definition.FieldType)))
        {
            var rawValue = overridesByKey.TryGetValue(definition.SettingKey, out var overrideJson)
                ? overrideJson
                : definition.DefaultValueJson;

            var normalized = NormalizeJsonValue(rawValue);
            if (normalized is not null)
            {
                valuesByKey[definition.SettingKey] = normalized;
            }
        }

        return new WorkspacePublicTheme(workspace.ThemePluginId, workspace.ThemeVersion, valuesByKey);
    }

    private static string? NormalizeJsonValue(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            var element = JsonSerializer.Deserialize<JsonElement>(json);
            return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString(),
                JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => element.GetRawText(),
                _ => null
            };
        }
        catch (JsonException)
        {
            // Legacy plain-string values were stored unquoted.
            return json.Trim();
        }
    }
}
