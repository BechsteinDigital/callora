using Callora.Host.Backend.Application.Extensions;

namespace Callora.Host.Backend.Tests.Support;

internal sealed class InMemoryWorkspaceThemeSettingsStore : IWorkspaceThemeSettingsStore
{
    private readonly Dictionary<string, WorkspaceThemeSettingDefinitionSnapshot[]> _definitionsByPlugin =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, WorkspaceThemeSettingValueSnapshot[]> _valuesByWorkspacePlugin =
        new(StringComparer.OrdinalIgnoreCase);

    public Task<IReadOnlyList<WorkspaceThemeSettingDefinitionSnapshot>> ListDefinitionsAsync(
        string pluginId,
        string version,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var key = $"{pluginId.Trim()}::{version.Trim()}";
        if (!_definitionsByPlugin.TryGetValue(key, out var values))
        {
            return Task.FromResult<IReadOnlyList<WorkspaceThemeSettingDefinitionSnapshot>>(Array.Empty<WorkspaceThemeSettingDefinitionSnapshot>());
        }

        return Task.FromResult<IReadOnlyList<WorkspaceThemeSettingDefinitionSnapshot>>(values);
    }

    public Task<IReadOnlyList<WorkspaceThemeSettingDefinitionSnapshot>> ReplaceDefinitionsForPluginAsync(
        string pluginId,
        string version,
        IReadOnlyList<WorkspaceThemeSettingDefinitionInput> definitions,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var normalizedPluginId = pluginId.Trim();
        var normalizedVersion = version.Trim();
        var nowUtc = DateTimeOffset.UtcNow;

        var snapshots = definitions.Select(x => new WorkspaceThemeSettingDefinitionSnapshot(
                x.SettingKey.Trim(),
                normalizedPluginId,
                normalizedVersion,
                x.Label.Trim(),
                x.FieldType.Trim().ToLowerInvariant(),
                x.Description,
                x.DefaultValueJson,
                x.IsRequired,
                x.SortOrder,
                x.GroupName,
                x.OptionsJson,
                x.IsActive,
                nowUtc,
                nowUtc))
            .ToArray();

        _definitionsByPlugin[$"{normalizedPluginId}::{normalizedVersion}"] = snapshots;
        return Task.FromResult<IReadOnlyList<WorkspaceThemeSettingDefinitionSnapshot>>(snapshots);
    }

    public Task<IReadOnlyList<WorkspaceThemeSettingValueSnapshot>> ListWorkspaceValuesAsync(
        string workspaceKey,
        string pluginId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var key = $"{workspaceKey.Trim()}::{pluginId.Trim()}";
        if (!_valuesByWorkspacePlugin.TryGetValue(key, out var values))
        {
            return Task.FromResult<IReadOnlyList<WorkspaceThemeSettingValueSnapshot>>(Array.Empty<WorkspaceThemeSettingValueSnapshot>());
        }

        return Task.FromResult<IReadOnlyList<WorkspaceThemeSettingValueSnapshot>>(values);
    }

    public Task<IReadOnlyList<WorkspaceThemeSettingValueSnapshot>> ReplaceWorkspaceValuesAsync(
        string workspaceKey,
        string pluginId,
        IReadOnlyDictionary<string, string?> valuesByKey,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var normalizedWorkspaceKey = workspaceKey.Trim();
        var normalizedPluginId = pluginId.Trim();
        var nowUtc = DateTimeOffset.UtcNow;

        var snapshots = valuesByKey
            .Where(x => !string.IsNullOrWhiteSpace(x.Key) && !string.IsNullOrWhiteSpace(x.Value))
            .Select(x => new WorkspaceThemeSettingValueSnapshot(
                normalizedWorkspaceKey,
                normalizedPluginId,
                x.Key.Trim(),
                x.Value!,
                nowUtc))
            .ToArray();

        _valuesByWorkspacePlugin[$"{normalizedWorkspaceKey}::{normalizedPluginId}"] = snapshots;
        return Task.FromResult<IReadOnlyList<WorkspaceThemeSettingValueSnapshot>>(snapshots);
    }

    public Task ClearPluginDefinitionsAsync(
        string pluginId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var normalizedPluginId = pluginId.Trim();
        foreach (var key in _definitionsByPlugin.Keys.Where(x => x.StartsWith($"{normalizedPluginId}::", StringComparison.OrdinalIgnoreCase)).ToArray())
        {
            _definitionsByPlugin.Remove(key);
        }

        foreach (var key in _valuesByWorkspacePlugin.Keys.Where(x => x.EndsWith($"::{normalizedPluginId}", StringComparison.OrdinalIgnoreCase)).ToArray())
        {
            _valuesByWorkspacePlugin.Remove(key);
        }

        return Task.CompletedTask;
    }
}
