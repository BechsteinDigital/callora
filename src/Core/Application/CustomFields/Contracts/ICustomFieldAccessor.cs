namespace Callora.Core.Application.CustomFields.Contracts;

/// <summary>
/// Read/write access to custom field values on core entities for plugins.
/// Values are JSON-encoded strings.
/// </summary>
public interface ICustomFieldAccessor
{
    Task<IReadOnlyDictionary<string, string>> GetValuesAsync(
        string entityName,
        string entityId,
        CancellationToken cancellationToken = default);

    Task SetValuesAsync(
        string entityName,
        string entityId,
        IReadOnlyDictionary<string, string?> valuesByKey,
        CancellationToken cancellationToken = default);
}
