using Callora.Host.Backend.Application.Configuration;

namespace Callora.Host.Backend.Tests.Support;

/// <summary>
/// In-memory system config store for resolver and endpoint tests.
/// </summary>
public sealed class InMemorySystemConfigStore : ISystemConfigStore
{
    private readonly List<SystemConfigDefinitionSnapshot> _definitions = [];
    private readonly List<SystemConfigValueSnapshot> _values = [];

    public Task<IReadOnlyList<SystemConfigDefinitionSnapshot>> ListDefinitionsAsync(
        string? pluginId = null,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<SystemConfigDefinitionSnapshot> result = _definitions
            .Where(definition => pluginId is null ||
                string.Equals(definition.PluginId, pluginId.Trim(), StringComparison.OrdinalIgnoreCase))
            .ToArray();
        return Task.FromResult(result);
    }

    public Task ReplaceDefinitionsForPluginAsync(
        string pluginId,
        string version,
        IReadOnlyList<SystemConfigDefinitionInput> definitions,
        CancellationToken cancellationToken = default)
    {
        _definitions.RemoveAll(definition =>
            string.Equals(definition.PluginId, pluginId, StringComparison.OrdinalIgnoreCase));
        _definitions.AddRange(definitions.Select(input => new SystemConfigDefinitionSnapshot(
            pluginId,
            version,
            input.ConfigKey,
            input.Label,
            input.FieldType,
            input.Description,
            input.DefaultValueJson,
            input.GroupName,
            input.OptionsJson,
            input.SortOrder,
            input.IsActive)));
        return Task.CompletedTask;
    }

    public Task ClearDefinitionsForPluginAsync(string pluginId, CancellationToken cancellationToken = default)
    {
        _definitions.RemoveAll(definition =>
            string.Equals(definition.PluginId, pluginId, StringComparison.OrdinalIgnoreCase));
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<SystemConfigValueSnapshot>> ListValuesAsync(
        string pluginId,
        IReadOnlyList<(string Scope, string ScopeKey)> scopeChain,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<SystemConfigValueSnapshot> result = _values
            .Where(value =>
                string.Equals(value.PluginId, pluginId, StringComparison.OrdinalIgnoreCase) &&
                scopeChain.Any(scope =>
                    value.Scope == scope.Scope &&
                    string.Equals(value.ScopeKey, scope.ScopeKey, StringComparison.OrdinalIgnoreCase)))
            .ToArray();
        return Task.FromResult(result);
    }

    public Task UpsertValuesAsync(
        string pluginId,
        string scope,
        string scopeKey,
        IReadOnlyDictionary<string, string?> valuesByKey,
        CancellationToken cancellationToken = default)
    {
        foreach (var (configKey, valueJson) in valuesByKey)
        {
            _values.RemoveAll(value =>
                string.Equals(value.PluginId, pluginId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(value.ConfigKey, configKey, StringComparison.OrdinalIgnoreCase) &&
                value.Scope == scope &&
                string.Equals(value.ScopeKey, scopeKey, StringComparison.OrdinalIgnoreCase));

            if (valueJson is not null)
            {
                _values.Add(new SystemConfigValueSnapshot(
                    pluginId,
                    configKey,
                    scope,
                    scopeKey,
                    valueJson,
                    DateTimeOffset.UtcNow));
            }
        }

        return Task.CompletedTask;
    }
}
