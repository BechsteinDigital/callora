using Callora.Core.Application.Configuration;
using Callora.Core.Application.Workspaces;
using System.Text.Json;

namespace Callora.Core.Application.Extensions;

/// <summary>
/// Resolves the effective theme setting values that a rendered surface sees.
/// <para>
/// Two levels compose: the workspace carries the baseline theme and its values,
/// a surface may override both. The cascade is
/// <c>theme default → workspace value → surface value</c>, mirroring the
/// configuration scopes.
/// </para>
/// <para>
/// A surface that assigns a <em>different</em> theme than its workspace does not
/// inherit the workspace values: those were entered for another theme's settings
/// and would carry over meaningless keys. Such a surface starts from the theme
/// defaults plus its own values.
/// </para>
/// </summary>
public sealed class WorkspacePublicThemeResolver(
    IWorkspaceManagementStore workspaceStore,
    IWorkspaceSurfaceStore surfaceStore,
    IWorkspaceThemeSettingsStore settingsStore)
{
    /// <summary>
    /// The workspace-level theme, without any surface override. Used where no
    /// surface is in play.
    /// </summary>
    public Task<WorkspacePublicTheme?> ResolveAsync(
        string workspaceKey,
        CancellationToken cancellationToken = default) =>
        ResolveForSurfaceAsync(workspaceKey, surfaceKey: null, cancellationToken);

    /// <summary>
    /// The effective theme for one surface of the workspace. Passing a null or
    /// unknown surface key falls back to the workspace level.
    /// </summary>
    public async Task<WorkspacePublicTheme?> ResolveForSurfaceAsync(
        string workspaceKey,
        string? surfaceKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceKey);

        var workspace = await workspaceStore
            .GetAsync(workspaceKey.Trim(), cancellationToken)
            .ConfigureAwait(false);
        if (workspace is null || !workspace.IsActive || !workspace.TenantIsActive)
        {
            return null;
        }

        var surface = string.IsNullOrWhiteSpace(surfaceKey)
            ? null
            : await surfaceStore
                .GetAsync(workspace.WorkspaceKey, surfaceKey.Trim(), cancellationToken)
                .ConfigureAwait(false);

        // The surface decides which theme applies; without its own it uses the
        // workspace's.
        var themePluginId = FirstSet(surface?.ThemePluginId, workspace.ThemePluginId);
        var themeVersion = FirstSet(surface?.ThemeVersion, workspace.ThemeVersion);
        if (themePluginId is null || themeVersion is null)
        {
            return null;
        }

        var definitions = await settingsStore
            .ListDefinitionsAsync(themePluginId, themeVersion, cancellationToken)
            .ConfigureAwait(false);

        // Workspace values only carry over while both levels run the same theme.
        var inheritsWorkspaceValues = IsSameTheme(workspace.ThemePluginId, themePluginId);
        var workspaceValues = inheritsWorkspaceValues
            ? await settingsStore
                .ListValuesAsync(workspace.WorkspaceKey, surfaceKey: null, themePluginId, cancellationToken)
                .ConfigureAwait(false)
            : [];
        var surfaceValues = surface is null
            ? []
            : await settingsStore
                .ListValuesAsync(workspace.WorkspaceKey, surface.SurfaceKey, themePluginId, cancellationToken)
                .ConfigureAwait(false);

        var overridesByKey = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var value in workspaceValues)
        {
            overridesByKey[value.SettingKey] = value.ValueJson;
        }

        // Applied last, so the surface wins over the workspace.
        foreach (var value in surfaceValues)
        {
            overridesByKey[value.SettingKey] = value.ValueJson;
        }

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

        return new WorkspacePublicTheme(themePluginId, themeVersion, valuesByKey);
    }

    private static string? FirstSet(string? preferred, string? fallback)
    {
        if (!string.IsNullOrWhiteSpace(preferred))
        {
            return preferred;
        }

        return string.IsNullOrWhiteSpace(fallback) ? null : fallback;
    }

    private static bool IsSameTheme(string? workspaceThemePluginId, string effectiveThemePluginId) =>
        !string.IsNullOrWhiteSpace(workspaceThemePluginId) &&
        string.Equals(workspaceThemePluginId, effectiveThemePluginId, StringComparison.OrdinalIgnoreCase);

    private static string? NormalizeJsonValue(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

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
