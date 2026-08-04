using Callora.Core.Application.Extensions;

namespace Callora.Core.Tests.Support;

internal sealed class InMemoryWorkspaceThemeSettingsStore : IWorkspaceThemeSettingsStore
{
    private readonly Dictionary<string, WorkspaceThemeSettingDefinitionSnapshot[]> _definitionsByPlugin =
        new(StringComparer.OrdinalIgnoreCase);
    // Keyed workspace::surface::plugin — the empty surface segment is the
    // workspace level, mirroring the EF store's empty surface_key.
    private readonly Dictionary<string, WorkspaceThemeSettingValueSnapshot[]> _valuesByLevel =
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

    public Task<IReadOnlyList<WorkspaceThemeSettingValueSnapshot>> ListValuesAsync(
        string workspaceKey,
        string? surfaceKey,
        string pluginId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_valuesByLevel.TryGetValue(LevelKey(workspaceKey, surfaceKey, pluginId), out var values))
        {
            return Task.FromResult<IReadOnlyList<WorkspaceThemeSettingValueSnapshot>>(Array.Empty<WorkspaceThemeSettingValueSnapshot>());
        }

        return Task.FromResult<IReadOnlyList<WorkspaceThemeSettingValueSnapshot>>(values);
    }

    public Task<IReadOnlyList<WorkspaceThemeSettingValueSnapshot>> ReplaceValuesAsync(
        string workspaceKey,
        string? surfaceKey,
        string pluginId,
        IReadOnlyDictionary<string, string?> valuesByKey,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var normalizedWorkspaceKey = workspaceKey.Trim();
        var normalizedSurfaceKey = NormalizeSurfaceKey(surfaceKey);
        var normalizedPluginId = pluginId.Trim();
        var nowUtc = DateTimeOffset.UtcNow;

        var snapshots = valuesByKey
            .Where(x => !string.IsNullOrWhiteSpace(x.Key) && !string.IsNullOrWhiteSpace(x.Value))
            .Select(x => new WorkspaceThemeSettingValueSnapshot(
                normalizedWorkspaceKey,
                normalizedSurfaceKey,
                normalizedPluginId,
                x.Key.Trim(),
                x.Value!,
                nowUtc))
            .ToArray();

        _valuesByLevel[LevelKey(normalizedWorkspaceKey, normalizedSurfaceKey, normalizedPluginId)] = snapshots;
        return Task.FromResult<IReadOnlyList<WorkspaceThemeSettingValueSnapshot>>(snapshots);
    }

    private static string NormalizeSurfaceKey(string? surfaceKey) =>
        string.IsNullOrWhiteSpace(surfaceKey) ? string.Empty : surfaceKey.Trim();

    private static string LevelKey(string workspaceKey, string? surfaceKey, string pluginId) =>
        $"{workspaceKey.Trim()}::{NormalizeSurfaceKey(surfaceKey)}::{pluginId.Trim()}";

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

        foreach (var key in _valuesByLevel.Keys.Where(x => x.EndsWith($"::{normalizedPluginId}", StringComparison.OrdinalIgnoreCase)).ToArray())
        {
            _valuesByLevel.Remove(key);
        }

        return Task.CompletedTask;
    }
}
