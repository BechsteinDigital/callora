using Callora.Core.Application.Workspaces;

namespace Callora.Core.Application.Extensions;

/// <summary>
/// Theme assignment and settings for a single surface — the level below the
/// workspace, in the sense a sales channel has its own look while sharing the
/// shop's baseline.
/// <para>
/// A surface may run the workspace's theme with its own values, or a different
/// theme entirely. In the second case the workspace values are not inherited:
/// they were entered for another theme's setting keys.
/// </para>
/// </summary>
public sealed class SurfaceThemeService(
    IWorkspaceManagementStore workspaceStore,
    IWorkspaceSurfaceStore surfaceStore,
    IWorkspaceTemplateRegistryStore templateStore,
    IWorkspaceThemeSettingsStore settingsStore)
{
    /// <summary>Surface for which a theme definition must be registered.</summary>
    private const string ThemeSurface = "surface";

    public async Task<SurfaceThemeAssignmentResult> GetAssignmentAsync(
        string workspaceKey,
        string surfaceKey,
        CancellationToken cancellationToken = default)
    {
        var context = await LoadAsync(workspaceKey, surfaceKey, cancellationToken).ConfigureAwait(false);
        if (context.Status != SurfaceThemeStatus.Ok)
        {
            return new SurfaceThemeAssignmentResult(context.Status, Message: context.Message);
        }

        return new SurfaceThemeAssignmentResult(SurfaceThemeStatus.Ok, ToAssignment(context));
    }

    public async Task<SurfaceThemeAssignmentResult> AssignAsync(
        string workspaceKey,
        string surfaceKey,
        string themePluginId,
        string themeVersion,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(themePluginId);
        ArgumentException.ThrowIfNullOrWhiteSpace(themeVersion);

        var context = await LoadAsync(workspaceKey, surfaceKey, cancellationToken).ConfigureAwait(false);
        if (context.Status != SurfaceThemeStatus.Ok)
        {
            return new SurfaceThemeAssignmentResult(context.Status, Message: context.Message);
        }

        var definitions = await templateStore
            .ListDefinitionsAsync(ThemeSurface, isActive: true, cancellationToken)
            .ConfigureAwait(false);
        var exists = definitions.Any(definition =>
            string.Equals(definition.PluginId, themePluginId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(definition.Version, themeVersion, StringComparison.OrdinalIgnoreCase));
        if (!exists)
        {
            return new SurfaceThemeAssignmentResult(
                SurfaceThemeStatus.ThemeNotFound,
                Message: $"No active workspace theme definitions found for {themePluginId}@{themeVersion}.");
        }

        return await StoreThemeAsync(context, themePluginId, themeVersion, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Drops the surface's own theme so it follows the workspace again. Its
    /// stored values are removed with it — they belong to the theme that was
    /// just detached and would silently reappear on a later re-assignment.
    /// </summary>
    public async Task<SurfaceThemeAssignmentResult> ClearAsync(
        string workspaceKey,
        string surfaceKey,
        CancellationToken cancellationToken = default)
    {
        var context = await LoadAsync(workspaceKey, surfaceKey, cancellationToken).ConfigureAwait(false);
        if (context.Status != SurfaceThemeStatus.Ok)
        {
            return new SurfaceThemeAssignmentResult(context.Status, Message: context.Message);
        }

        var previousThemePluginId = context.Surface!.ThemePluginId;
        var result = await StoreThemeAsync(context, themePluginId: null, themeVersion: null, cancellationToken)
            .ConfigureAwait(false);
        if (result.Status == SurfaceThemeStatus.Ok && !string.IsNullOrWhiteSpace(previousThemePluginId))
        {
            await settingsStore
                .ReplaceValuesAsync(
                    context.Workspace!.WorkspaceKey,
                    context.Surface.SurfaceKey,
                    previousThemePluginId,
                    new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return result;
    }

    public async Task<SurfaceThemeSettingsResult> GetSettingsAsync(
        string workspaceKey,
        string surfaceKey,
        CancellationToken cancellationToken = default)
    {
        var context = await LoadAsync(workspaceKey, surfaceKey, cancellationToken).ConfigureAwait(false);
        if (context.Status != SurfaceThemeStatus.Ok)
        {
            return new SurfaceThemeSettingsResult(context.Status, Message: context.Message);
        }

        return new SurfaceThemeSettingsResult(
            SurfaceThemeStatus.Ok,
            await BuildSettingsAsync(context, cancellationToken).ConfigureAwait(false));
    }

    public async Task<SurfaceThemeSettingsResult> ReplaceSettingsAsync(
        string workspaceKey,
        string surfaceKey,
        IReadOnlyDictionary<string, string?> valuesByKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(valuesByKey);

        var context = await LoadAsync(workspaceKey, surfaceKey, cancellationToken).ConfigureAwait(false);
        if (context.Status != SurfaceThemeStatus.Ok)
        {
            return new SurfaceThemeSettingsResult(context.Status, Message: context.Message);
        }

        var themePluginId = context.EffectiveThemePluginId;
        var themeVersion = context.EffectiveThemeVersion;
        if (themePluginId is null || themeVersion is null)
        {
            return new SurfaceThemeSettingsResult(
                SurfaceThemeStatus.NoThemeAssigned,
                Message: "Neither the surface nor its workspace has a theme assigned.");
        }

        // Only keys the active theme actually declares are stored; anything else
        // would linger as an orphan the editor never shows again.
        var definitions = await settingsStore
            .ListDefinitionsAsync(themePluginId, themeVersion, cancellationToken)
            .ConfigureAwait(false);
        var allowedKeys = definitions
            .Where(definition => definition.IsActive)
            .Select(definition => definition.SettingKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var accepted = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in valuesByKey)
        {
            if (!string.IsNullOrWhiteSpace(key) && allowedKeys.Contains(key.Trim()))
            {
                accepted[key.Trim()] = value;
            }
        }

        await settingsStore
            .ReplaceValuesAsync(
                context.Workspace!.WorkspaceKey,
                context.Surface!.SurfaceKey,
                themePluginId,
                accepted,
                cancellationToken)
            .ConfigureAwait(false);

        return new SurfaceThemeSettingsResult(
            SurfaceThemeStatus.Ok,
            await BuildSettingsAsync(context, cancellationToken).ConfigureAwait(false));
    }

    private async Task<SurfaceThemeContext> LoadAsync(
        string workspaceKey,
        string surfaceKey,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(surfaceKey);

        var workspace = await workspaceStore
            .GetAsync(workspaceKey.Trim(), cancellationToken)
            .ConfigureAwait(false);
        if (workspace is null)
        {
            return new SurfaceThemeContext(
                SurfaceThemeStatus.WorkspaceNotFound,
                Message: $"Workspace '{workspaceKey}' not found.");
        }

        var surface = await surfaceStore
            .GetAsync(workspace.WorkspaceKey, surfaceKey.Trim(), cancellationToken)
            .ConfigureAwait(false);
        if (surface is null)
        {
            return new SurfaceThemeContext(
                SurfaceThemeStatus.SurfaceNotFound,
                Message: $"Surface '{surfaceKey}' not found in workspace '{workspaceKey}'.");
        }

        return new SurfaceThemeContext(SurfaceThemeStatus.Ok, workspace, surface);
    }

    // The surface upsert is a full replace, so every field travels back
    // untouched — only the two theme columns change.
    private async Task<SurfaceThemeAssignmentResult> StoreThemeAsync(
        SurfaceThemeContext context,
        string? themePluginId,
        string? themeVersion,
        CancellationToken cancellationToken)
    {
        var surface = context.Surface!;
        var stored = await surfaceStore
            .UpsertAsync(
                context.Workspace!.WorkspaceKey,
                new WorkspaceSurfaceInput(
                    surface.SurfaceKey,
                    surface.DisplayName,
                    surface.SurfaceType,
                    surface.PublicBaseUrl,
                    surface.PublicHost,
                    surface.PublicPathPrefix,
                    surface.Authentication,
                    surface.Locale,
                    surface.TemplatePluginId,
                    surface.TemplateVersion,
                    themePluginId,
                    themeVersion,
                    surface.IsActive),
                cancellationToken)
            .ConfigureAwait(false);
        if (stored is null)
        {
            return new SurfaceThemeAssignmentResult(
                SurfaceThemeStatus.WorkspaceNotFound,
                Message: $"Workspace '{context.Workspace.WorkspaceKey}' not found.");
        }

        return new SurfaceThemeAssignmentResult(
            SurfaceThemeStatus.Ok,
            ToAssignment(context with { Surface = stored }));
    }

    private async Task<SurfaceThemeSettings> BuildSettingsAsync(
        SurfaceThemeContext context,
        CancellationToken cancellationToken)
    {
        var themePluginId = context.EffectiveThemePluginId;
        var themeVersion = context.EffectiveThemeVersion;
        if (themePluginId is null || themeVersion is null)
        {
            return new SurfaceThemeSettings(
                context.Workspace!.WorkspaceKey,
                context.Surface!.SurfaceKey,
                HasAssignedTheme: false,
                ThemePluginId: null,
                ThemeVersion: null,
                InheritedFromWorkspace: true,
                InheritsWorkspaceValues: false,
                Fields: [],
                OwnValuesByKey: EmptyValues(),
                InheritedValuesByKey: EmptyValues());
        }

        var definitions = await settingsStore
            .ListDefinitionsAsync(themePluginId, themeVersion, cancellationToken)
            .ConfigureAwait(false);
        var ownValues = await settingsStore
            .ListValuesAsync(context.Workspace!.WorkspaceKey, context.Surface!.SurfaceKey, themePluginId, cancellationToken)
            .ConfigureAwait(false);

        var inheritsWorkspaceValues = context.InheritsWorkspaceValues;
        var inheritedValues = inheritsWorkspaceValues
            ? await settingsStore
                .ListValuesAsync(context.Workspace.WorkspaceKey, surfaceKey: null, themePluginId, cancellationToken)
                .ConfigureAwait(false)
            : [];

        return new SurfaceThemeSettings(
            context.Workspace.WorkspaceKey,
            context.Surface.SurfaceKey,
            HasAssignedTheme: true,
            themePluginId,
            themeVersion,
            context.InheritedFromWorkspace,
            inheritsWorkspaceValues,
            definitions,
            ToValueMap(ownValues),
            ToValueMap(inheritedValues));
    }

    private static SurfaceThemeAssignment ToAssignment(SurfaceThemeContext context) =>
        new(
            context.Workspace!.WorkspaceKey,
            context.Surface!.SurfaceKey,
            context.EffectiveThemePluginId,
            context.EffectiveThemeVersion,
            context.InheritedFromWorkspace);

    private static IReadOnlyDictionary<string, string> ToValueMap(
        IReadOnlyList<WorkspaceThemeSettingValueSnapshot> values) =>
        values.ToDictionary(
            static value => value.SettingKey,
            static value => value.ValueJson,
            StringComparer.OrdinalIgnoreCase);

    private static IReadOnlyDictionary<string, string> EmptyValues() =>
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}
