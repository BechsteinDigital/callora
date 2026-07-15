using Callora.Core.Application.CustomFields;
using Callora.Core.Domain.CustomFields;
using Microsoft.EntityFrameworkCore;

namespace Callora.Core.Infrastructure.Persistence;

public sealed class EfCustomFieldStore(HostPersistenceDbContext dbContext) : ICustomFieldStore
{
    public async Task<IReadOnlyList<CustomFieldDefinitionSnapshot>> ListDefinitionsAsync(
        string? entityName = null,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.CustomFieldDefinitions.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(entityName))
        {
            var normalized = entityName.Trim().ToLowerInvariant();
            query = query.Where(x => x.EntityName == normalized);
        }

        return await query
            .OrderBy(x => x.EntityName)
            .ThenBy(x => x.SortOrder)
            .Select(x => new CustomFieldDefinitionSnapshot(
                x.PluginId, x.Version, x.EntityName, x.FieldKey, x.Label, x.FieldType, x.SortOrder, x.IsActive))
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task ReplaceDefinitionsForPluginAsync(
        string pluginId,
        string version,
        IReadOnlyList<CustomFieldDefinitionSnapshot> definitions,
        CancellationToken cancellationToken = default)
    {
        var normalized = pluginId.Trim();
        var existing = await dbContext.CustomFieldDefinitions
            .Where(x => x.PluginId == normalized)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        dbContext.CustomFieldDefinitions.RemoveRange(existing);

        var now = DateTimeOffset.UtcNow;
        foreach (var definition in definitions)
        {
            dbContext.CustomFieldDefinitions.Add(new CustomFieldDefinition
            {
                Id = Guid.NewGuid(),
                PluginId = normalized,
                Version = version.Trim(),
                EntityName = definition.EntityName.Trim().ToLowerInvariant(),
                FieldKey = definition.FieldKey.Trim(),
                Label = definition.Label,
                FieldType = definition.FieldType,
                SortOrder = definition.SortOrder,
                IsActive = definition.IsActive,
                CreatedAtUtc = now
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task ClearDefinitionsForPluginAsync(string pluginId, CancellationToken cancellationToken = default)
    {
        var normalized = pluginId.Trim();
        var existing = await dbContext.CustomFieldDefinitions
            .Where(x => x.PluginId == normalized)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        dbContext.CustomFieldDefinitions.RemoveRange(existing);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyDictionary<string, string>> GetValuesAsync(
        string entityName,
        string entityId,
        CancellationToken cancellationToken = default)
    {
        var normalizedEntity = entityName.Trim().ToLowerInvariant();
        var normalizedId = entityId.Trim();
        return await dbContext.CustomFieldValues
            .AsNoTracking()
            .Where(x => x.EntityName == normalizedEntity && x.EntityId == normalizedId)
            .ToDictionaryAsync(x => x.FieldKey, x => x.ValueJson, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task SetValuesAsync(
        string entityName,
        string entityId,
        IReadOnlyDictionary<string, string?> valuesByKey,
        CancellationToken cancellationToken = default)
    {
        var normalizedEntity = entityName.Trim().ToLowerInvariant();
        var normalizedId = entityId.Trim();

        // Workspace-Entitäten tragen ihren Besitzer als Spalte, damit die
        // Workspace-Löschkaskade die Werte findet (PLAT-245).
        var workspaceKey = string.Equals(normalizedEntity, "workspace", StringComparison.Ordinal)
            ? normalizedId
            : null;

        var existing = await dbContext.CustomFieldValues
            .Where(x => x.EntityName == normalizedEntity && x.EntityId == normalizedId)
            .ToDictionaryAsync(x => x.FieldKey, StringComparer.OrdinalIgnoreCase, cancellationToken)
            .ConfigureAwait(false);

        var now = DateTimeOffset.UtcNow;
        foreach (var (fieldKey, valueJson) in valuesByKey)
        {
            var normalizedKey = fieldKey.Trim();
            if (valueJson is null)
            {
                if (existing.TryGetValue(normalizedKey, out var toRemove))
                {
                    dbContext.CustomFieldValues.Remove(toRemove);
                }
                continue;
            }

            if (existing.TryGetValue(normalizedKey, out var current))
            {
                current.ValueJson = valueJson;
                current.WorkspaceKey = workspaceKey;
                current.UpdatedAtUtc = now;
            }
            else
            {
                dbContext.CustomFieldValues.Add(new CustomFieldValue
                {
                    Id = Guid.NewGuid(),
                    EntityName = normalizedEntity,
                    EntityId = normalizedId,
                    FieldKey = normalizedKey,
                    ValueJson = valueJson,
                    WorkspaceKey = workspaceKey,
                    UpdatedAtUtc = now
                });
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
