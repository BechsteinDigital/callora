using Callora.Core.Application.Data.Contracts;
using Callora.Core.Domain.Plugins;
using Microsoft.EntityFrameworkCore;

namespace Callora.Core.Infrastructure.Persistence;

/// <summary>
/// PostgreSQL-backed plugin data store (jsonb documents).
/// </summary>
public sealed class EfPluginDataStore(HostPersistenceDbContext dbContext) : IPluginDataStore
{
    public async Task<string?> GetAsync(PluginDataKey key, CancellationToken cancellationToken = default)
    {
        var (pluginId, workspaceKey, collection, entryKey) = NormalizeKey(key);

        var document = await dbContext.PluginDataDocuments
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.PluginId == pluginId &&
                     x.WorkspaceKey == workspaceKey &&
                     x.Collection == collection &&
                     x.EntryKey == entryKey,
                cancellationToken)
            .ConfigureAwait(false);

        return document?.JsonDocument;
    }

    public async Task SetAsync(PluginDataKey key, string jsonDocument, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(jsonDocument);
        var (pluginId, workspaceKey, collection, entryKey) = NormalizeKey(key);
        var nowUtc = DateTimeOffset.UtcNow;

        var document = await dbContext.PluginDataDocuments
            .SingleOrDefaultAsync(
                x => x.PluginId == pluginId &&
                     x.WorkspaceKey == workspaceKey &&
                     x.Collection == collection &&
                     x.EntryKey == entryKey,
                cancellationToken)
            .ConfigureAwait(false);

        if (document is null)
        {
            document = PluginDataDocument.Create(pluginId, workspaceKey, collection, entryKey, jsonDocument, nowUtc);
            dbContext.PluginDataDocuments.Add(document);
        }
        else
        {
            document.UpdateDocument(jsonDocument, nowUtc);
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> RemoveAsync(PluginDataKey key, CancellationToken cancellationToken = default)
    {
        var (pluginId, workspaceKey, collection, entryKey) = NormalizeKey(key);

        var deleted = await dbContext.PluginDataDocuments
            .Where(x => x.PluginId == pluginId &&
                        x.WorkspaceKey == workspaceKey &&
                        x.Collection == collection &&
                        x.EntryKey == entryKey)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        return deleted > 0;
    }

    public async Task<IReadOnlyList<PluginDataEntry>> ListAsync(
        PluginDataCollectionKey collection,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(collection);
        var pluginId = NormalizeRequired(collection.PluginId, nameof(collection.PluginId));
        var workspaceKey = NormalizeWorkspace(collection.WorkspaceKey);
        var collectionName = NormalizeRequired(collection.Collection, nameof(collection.Collection));

        return await dbContext.PluginDataDocuments
            .AsNoTracking()
            .Where(x => x.PluginId == pluginId &&
                        x.WorkspaceKey == workspaceKey &&
                        x.Collection == collectionName)
            .OrderBy(x => x.EntryKey)
            .Select(x => new PluginDataEntry(x.EntryKey, x.JsonDocument, x.UpdatedAtUtc))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<string>> ListWorkspaceKeysAsync(
        string pluginId,
        string collection,
        CancellationToken cancellationToken = default)
    {
        var normalizedPluginId = NormalizeRequired(pluginId, nameof(pluginId));
        var normalizedCollection = NormalizeRequired(collection, nameof(collection));

        return await dbContext.PluginDataDocuments
            .AsNoTracking()
            .Where(x => x.PluginId == normalizedPluginId &&
                        x.Collection == normalizedCollection &&
                        x.WorkspaceKey != string.Empty)
            .Select(x => x.WorkspaceKey)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private static (string PluginId, string WorkspaceKey, string Collection, string EntryKey) NormalizeKey(PluginDataKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return (
            NormalizeRequired(key.PluginId, nameof(key.PluginId)),
            NormalizeWorkspace(key.WorkspaceKey),
            NormalizeRequired(key.Collection, nameof(key.Collection)),
            NormalizeRequired(key.EntryKey, nameof(key.EntryKey)));
    }

    private static string NormalizeRequired(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        return value.Trim();
    }

    private static string NormalizeWorkspace(string? workspaceKey) =>
        string.IsNullOrWhiteSpace(workspaceKey) ? string.Empty : workspaceKey.Trim();
}
