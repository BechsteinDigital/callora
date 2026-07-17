namespace Callora.Core.Application.CustomFields.Contracts;

/// <summary>
/// Read/write access to custom field values on core entities for plugins.
/// Values are JSON-encoded strings.
/// </summary>
public interface ICustomFieldAccessor
{
    /// <summary>
    /// Returns the custom field values set on the entity, keyed by field key.
    /// </summary>
    Task<IReadOnlyDictionary<string, string>> GetValuesAsync(
        string entityName,
        string entityId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes the given field values on the entity. A null value clears the field.
    /// </summary>
    Task SetValuesAsync(
        string entityName,
        string entityId,
        IReadOnlyDictionary<string, string?> valuesByKey,
        CancellationToken cancellationToken = default);
}
