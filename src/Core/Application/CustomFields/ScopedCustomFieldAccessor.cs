using Callora.Core.Application.CustomFields;
using Callora.Core.Application.CustomFields.Contracts;

namespace Callora.Core.Application.CustomFields;

/// <summary>
/// Singleton custom-field facade for plugins over the scoped store.
/// </summary>
public sealed class ScopedCustomFieldAccessor(IServiceScopeFactory scopeFactory) : ICustomFieldAccessor
{
    public async Task<IReadOnlyDictionary<string, string>> GetValuesAsync(
        string entityName,
        string entityId,
        CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<ICustomFieldStore>();
        return await store.GetValuesAsync(entityName, entityId, cancellationToken).ConfigureAwait(false);
    }

    public async Task SetValuesAsync(
        string entityName,
        string entityId,
        IReadOnlyDictionary<string, string?> valuesByKey,
        CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<ICustomFieldStore>();
        await store.SetValuesAsync(entityName, entityId, valuesByKey, cancellationToken).ConfigureAwait(false);
    }
}
