namespace Callora.Host.Backend.Application.Abstractions.CustomFields;

public interface ICustomFieldStore
{
    Task<IReadOnlyList<CustomFieldDefinitionSnapshot>> ListDefinitionsAsync(
        string? entityName = null,
        CancellationToken cancellationToken = default);

    Task ReplaceDefinitionsForPluginAsync(
        string pluginId,
        string version,
        IReadOnlyList<CustomFieldDefinitionSnapshot> definitions,
        CancellationToken cancellationToken = default);

    Task ClearDefinitionsForPluginAsync(string pluginId, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<string, string>> GetValuesAsync(
        string entityName,
        string entityId,
        CancellationToken cancellationToken = default);

    /// <summary>Null values delete the stored entry.</summary>
    Task SetValuesAsync(
        string entityName,
        string entityId,
        IReadOnlyDictionary<string, string?> valuesByKey,
        CancellationToken cancellationToken = default);
}
