using Callora.Core.Application.Data.Contracts;
using System.Collections.Concurrent;

namespace Callora.Core.Application.Plugins;

/// <summary>
/// Thread-safe in-memory plugin data store for tests and hosts without database.
/// </summary>
public sealed class InMemoryPluginDataStore : IPluginDataStore
{
    private readonly ConcurrentDictionary<(string PluginId, string WorkspaceKey, string Collection, string EntryKey), PluginDataEntry> _documents = new();

    public Task<string?> GetAsync(PluginDataKey key, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeKey(key);
        return Task.FromResult(_documents.TryGetValue(normalized, out var entry) ? entry.JsonDocument : null);
    }

    public Task SetAsync(PluginDataKey key, string jsonDocument, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(jsonDocument);
        var normalized = NormalizeKey(key);
        _documents[normalized] = new PluginDataEntry(normalized.EntryKey, jsonDocument, DateTimeOffset.UtcNow);
        return Task.CompletedTask;
    }

    public Task<bool> RemoveAsync(PluginDataKey key, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_documents.TryRemove(NormalizeKey(key), out _));
    }

    public Task<IReadOnlyList<PluginDataEntry>> ListAsync(
        PluginDataCollectionKey collection,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(collection);
        var pluginId = NormalizeRequired(collection.PluginId);
        var workspaceKey = NormalizeWorkspace(collection.WorkspaceKey);
        var collectionName = NormalizeRequired(collection.Collection);

        IReadOnlyList<PluginDataEntry> entries = _documents
            .Where(pair =>
                string.Equals(pair.Key.PluginId, pluginId, StringComparison.Ordinal) &&
                string.Equals(pair.Key.WorkspaceKey, workspaceKey, StringComparison.Ordinal) &&
                string.Equals(pair.Key.Collection, collectionName, StringComparison.Ordinal))
            .Select(pair => pair.Value)
            .OrderBy(entry => entry.EntryKey, StringComparer.Ordinal)
            .ToArray();

        return Task.FromResult(entries);
    }

    public Task<IReadOnlyList<string>> ListWorkspaceKeysAsync(
        string pluginId,
        string collection,
        CancellationToken cancellationToken = default)
    {
        var normalizedPluginId = NormalizeRequired(pluginId);
        var normalizedCollection = NormalizeRequired(collection);

        IReadOnlyList<string> workspaceKeys = _documents.Keys
            .Where(key =>
                string.Equals(key.PluginId, normalizedPluginId, StringComparison.Ordinal) &&
                string.Equals(key.Collection, normalizedCollection, StringComparison.Ordinal) &&
                key.WorkspaceKey.Length > 0)
            .Select(key => key.WorkspaceKey)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToArray();

        return Task.FromResult(workspaceKeys);
    }

    private static (string PluginId, string WorkspaceKey, string Collection, string EntryKey) NormalizeKey(PluginDataKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return (
            NormalizeRequired(key.PluginId),
            NormalizeWorkspace(key.WorkspaceKey),
            NormalizeRequired(key.Collection),
            NormalizeRequired(key.EntryKey));
    }

    private static string NormalizeRequired(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return value.Trim();
    }

    private static string NormalizeWorkspace(string? workspaceKey) =>
        string.IsNullOrWhiteSpace(workspaceKey) ? string.Empty : workspaceKey.Trim();
}
