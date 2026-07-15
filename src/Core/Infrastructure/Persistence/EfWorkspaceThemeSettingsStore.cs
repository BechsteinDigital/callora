using Callora.Core.Application.Extensions;
using Callora.Core.Domain.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Callora.Core.Infrastructure.Persistence;

public sealed class EfWorkspaceThemeSettingsStore(HostPersistenceDbContext dbContext) : IWorkspaceThemeSettingsStore
{
    public async Task<IReadOnlyList<WorkspaceThemeSettingDefinitionSnapshot>> ListDefinitionsAsync(
        string pluginId,
        string version,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        ArgumentException.ThrowIfNullOrWhiteSpace(version);

        var normalizedPluginId = pluginId.Trim();
        var normalizedVersion = version.Trim();

        return await dbContext.WorkspaceThemeSettingDefinitions
            .AsNoTracking()
            .Where(x => x.PluginId == normalizedPluginId && x.Version == normalizedVersion)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Label)
            .ThenBy(x => x.SettingKey)
            .Select(ToDefinitionSnapshotExpression())
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<WorkspaceThemeSettingDefinitionSnapshot>> ReplaceDefinitionsForPluginAsync(
        string pluginId,
        string version,
        IReadOnlyList<WorkspaceThemeSettingDefinitionInput> definitions,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        ArgumentNullException.ThrowIfNull(definitions);

        var normalizedPluginId = pluginId.Trim();
        var normalizedVersion = version.Trim();
        var nowUtc = DateTimeOffset.UtcNow;

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        await dbContext.WorkspaceThemeSettingDefinitions
            .Where(x => x.PluginId == normalizedPluginId && x.Version == normalizedVersion)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        var entities = definitions.Select(x => new WorkspaceThemeSettingDefinition
            {
                Id = Guid.NewGuid(),
                SettingKey = x.SettingKey.Trim(),
                PluginId = normalizedPluginId,
                Version = normalizedVersion,
                Label = x.Label.Trim(),
                FieldType = x.FieldType.Trim().ToLowerInvariant(),
                Description = string.IsNullOrWhiteSpace(x.Description) ? null : x.Description.Trim(),
                DefaultValueJson = x.DefaultValueJson,
                IsRequired = x.IsRequired,
                SortOrder = x.SortOrder,
                GroupName = string.IsNullOrWhiteSpace(x.GroupName) ? null : x.GroupName.Trim(),
                OptionsJson = x.OptionsJson,
                IsActive = x.IsActive,
                CreatedAtUtc = nowUtc,
                UpdatedAtUtc = nowUtc
            })
            .ToArray();

        if (entities.Length > 0)
        {
            await dbContext.WorkspaceThemeSettingDefinitions.AddRangeAsync(entities, cancellationToken).ConfigureAwait(false);
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        var knownPluginSettingKeys = await dbContext.WorkspaceThemeSettingDefinitions
            .AsNoTracking()
            .Where(x => x.PluginId == normalizedPluginId)
            .Select(x => x.SettingKey)
            .Distinct()
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);

        if (knownPluginSettingKeys.Length == 0)
        {
            await dbContext.WorkspaceThemeSettingValues
                .Where(x => x.PluginId == normalizedPluginId)
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            await dbContext.WorkspaceThemeSettingValues
                .Where(x => x.PluginId == normalizedPluginId && !knownPluginSettingKeys.Contains(x.SettingKey))
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        return entities.Select(ToDefinitionSnapshot).ToArray();
    }

    public async Task<IReadOnlyList<WorkspaceThemeSettingValueSnapshot>> ListWorkspaceValuesAsync(
        string workspaceKey,
        string pluginId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);

        var normalizedWorkspaceKey = workspaceKey.Trim();
        var normalizedPluginId = pluginId.Trim();

        return await dbContext.WorkspaceThemeSettingValues
            .AsNoTracking()
            .Where(x => x.WorkspaceKey == normalizedWorkspaceKey && x.PluginId == normalizedPluginId)
            .OrderBy(x => x.SettingKey)
            .Select(x => new WorkspaceThemeSettingValueSnapshot(
                x.WorkspaceKey,
                x.PluginId,
                x.SettingKey,
                x.ValueJson,
                x.UpdatedAtUtc))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<WorkspaceThemeSettingValueSnapshot>> ReplaceWorkspaceValuesAsync(
        string workspaceKey,
        string pluginId,
        IReadOnlyDictionary<string, string?> valuesByKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        ArgumentNullException.ThrowIfNull(valuesByKey);

        var normalizedWorkspaceKey = workspaceKey.Trim();
        var normalizedPluginId = pluginId.Trim();
        var nowUtc = DateTimeOffset.UtcNow;

        var existingRows = await dbContext.WorkspaceThemeSettingValues
            .Where(x => x.WorkspaceKey == normalizedWorkspaceKey && x.PluginId == normalizedPluginId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var normalizedInputs = valuesByKey
            .Where(x => !string.IsNullOrWhiteSpace(x.Key))
            .ToDictionary(
                x => x.Key.Trim(),
                x => string.IsNullOrWhiteSpace(x.Value) ? null : x.Value,
                StringComparer.OrdinalIgnoreCase);

        var requestedKeys = normalizedInputs.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var removedRows = existingRows.Where(x => !requestedKeys.Contains(x.SettingKey) || normalizedInputs[x.SettingKey] is null).ToArray();
        if (removedRows.Length > 0)
        {
            dbContext.WorkspaceThemeSettingValues.RemoveRange(removedRows);
        }

        foreach (var row in existingRows)
        {
            if (!requestedKeys.Contains(row.SettingKey))
            {
                continue;
            }

            var inputValue = normalizedInputs[row.SettingKey];
            if (inputValue is null)
            {
                continue;
            }

            row.ValueJson = inputValue;
            row.UpdatedAtUtc = nowUtc;
            requestedKeys.Remove(row.SettingKey);
        }

        foreach (var key in requestedKeys)
        {
            var valueJson = normalizedInputs[key];
            if (valueJson is null)
            {
                continue;
            }

            dbContext.WorkspaceThemeSettingValues.Add(new WorkspaceThemeSettingValue
            {
                Id = Guid.NewGuid(),
                WorkspaceKey = normalizedWorkspaceKey,
                PluginId = normalizedPluginId,
                SettingKey = key,
                ValueJson = valueJson,
                UpdatedAtUtc = nowUtc
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return await ListWorkspaceValuesAsync(normalizedWorkspaceKey, normalizedPluginId, cancellationToken).ConfigureAwait(false);
    }

    public async Task ClearPluginDefinitionsAsync(
        string pluginId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);

        var normalizedPluginId = pluginId.Trim();
        await dbContext.WorkspaceThemeSettingDefinitions
            .Where(x => x.PluginId == normalizedPluginId)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
        await dbContext.WorkspaceThemeSettingValues
            .Where(x => x.PluginId == normalizedPluginId)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private static WorkspaceThemeSettingDefinitionSnapshot ToDefinitionSnapshot(WorkspaceThemeSettingDefinition definition)
    {
        return new WorkspaceThemeSettingDefinitionSnapshot(
            definition.SettingKey,
            definition.PluginId,
            definition.Version,
            definition.Label,
            definition.FieldType,
            definition.Description,
            definition.DefaultValueJson,
            definition.IsRequired,
            definition.SortOrder,
            definition.GroupName,
            definition.OptionsJson,
            definition.IsActive,
            definition.CreatedAtUtc,
            definition.UpdatedAtUtc);
    }

    private static System.Linq.Expressions.Expression<Func<WorkspaceThemeSettingDefinition, WorkspaceThemeSettingDefinitionSnapshot>>
        ToDefinitionSnapshotExpression()
    {
        return x => new WorkspaceThemeSettingDefinitionSnapshot(
            x.SettingKey,
            x.PluginId,
            x.Version,
            x.Label,
            x.FieldType,
            x.Description,
            x.DefaultValueJson,
            x.IsRequired,
            x.SortOrder,
            x.GroupName,
            x.OptionsJson,
            x.IsActive,
            x.CreatedAtUtc,
            x.UpdatedAtUtc);
    }
}
