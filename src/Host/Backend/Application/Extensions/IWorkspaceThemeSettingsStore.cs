namespace Callora.Host.Backend.Application.Extensions;

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

    Task<IReadOnlyList<WorkspaceThemeSettingValueSnapshot>> ListWorkspaceValuesAsync(
        string workspaceKey,
        string pluginId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WorkspaceThemeSettingValueSnapshot>> ReplaceWorkspaceValuesAsync(
        string workspaceKey,
        string pluginId,
        IReadOnlyDictionary<string, string?> valuesByKey,
        CancellationToken cancellationToken = default);

    Task ClearPluginDefinitionsAsync(
        string pluginId,
        CancellationToken cancellationToken = default);
}
