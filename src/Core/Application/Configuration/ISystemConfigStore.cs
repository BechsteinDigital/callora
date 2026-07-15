namespace Callora.Core.Application.Configuration;

public interface ISystemConfigStore
{
    Task<IReadOnlyList<SystemConfigDefinitionSnapshot>> ListDefinitionsAsync(
        string? pluginId = null,
        CancellationToken cancellationToken = default);

    Task ReplaceDefinitionsForPluginAsync(
        string pluginId,
        string version,
        IReadOnlyList<SystemConfigDefinitionInput> definitions,
        CancellationToken cancellationToken = default);

    Task ClearDefinitionsForPluginAsync(
        string pluginId,
        CancellationToken cancellationToken = default);

    /// <summary>Lists stored values of one plugin across the given scope chain.</summary>
    Task<IReadOnlyList<SystemConfigValueSnapshot>> ListValuesAsync(
        string pluginId,
        IReadOnlyList<(string Scope, string ScopeKey)> scopeChain,
        CancellationToken cancellationToken = default);

    Task UpsertValuesAsync(
        string pluginId,
        string scope,
        string scopeKey,
        IReadOnlyDictionary<string, string?> valuesByKey,
        CancellationToken cancellationToken = default);
}
