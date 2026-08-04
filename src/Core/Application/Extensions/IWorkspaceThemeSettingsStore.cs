namespace Callora.Core.Application.Extensions;

/// <summary>
/// Persistence for theme setting definitions (what a theme offers) and their
/// values (what an operator chose).
/// <para>
/// Values live on two levels: the workspace, and optionally one of its surfaces.
/// A surface value overrides the workspace value for that surface only — the
/// same cascade the configuration scopes use. The level is addressed by
/// <c>surfaceKey</c>: null or empty means the workspace level.
/// </para>
/// </summary>
public interface IWorkspaceThemeSettingsStore
{
    Task<IReadOnlyList<WorkspaceThemeSettingDefinitionSnapshot>> ListDefinitionsAsync(
        string pluginId,
        string version,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WorkspaceThemeSettingDefinitionSnapshot>> ReplaceDefinitionsForPluginAsync(
        string pluginId,
        string version,
        IReadOnlyList<WorkspaceThemeSettingDefinitionInput> definitions,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Values stored at exactly one level — the workspace itself
    /// (<paramref name="surfaceKey"/> null/empty) or one surface. It never merges
    /// the two; composing the cascade is the resolver's job.
    /// </summary>
    Task<IReadOnlyList<WorkspaceThemeSettingValueSnapshot>> ListValuesAsync(
        string workspaceKey,
        string? surfaceKey,
        string pluginId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces the values of one level. A null entry removes the key, so it
    /// falls through to the next level (surface → workspace → theme default).
    /// </summary>
    Task<IReadOnlyList<WorkspaceThemeSettingValueSnapshot>> ReplaceValuesAsync(
        string workspaceKey,
        string? surfaceKey,
        string pluginId,
        IReadOnlyDictionary<string, string?> valuesByKey,
        CancellationToken cancellationToken = default);

    Task ClearPluginDefinitionsAsync(
        string pluginId,
        CancellationToken cancellationToken = default);
}
