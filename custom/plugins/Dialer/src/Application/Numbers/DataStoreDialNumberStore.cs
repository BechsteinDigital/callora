using Callora.Core.Application.Data.Contracts;
using System.Text.Json;

namespace Callora.Plugins.Dialer.Application.Numbers;

/// <summary>
/// Dial list persistence backed by the host-provided plugin data store.
/// </summary>
public sealed class DataStoreDialNumberStore(IPluginDataStore dataStore) : IDialNumberStore
{
    private const string Collection = "dial-numbers";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<DialNumberEntry>> ListAsync(
        string workspaceKey,
        CancellationToken cancellationToken = default)
    {
        var entries = await dataStore
            .ListAsync(new PluginDataCollectionKey(DialerPlugin.Id, workspaceKey, Collection), cancellationToken)
            .ConfigureAwait(false);

        return entries
            .Select(entry => Deserialize(entry.JsonDocument))
            .Where(static entry => entry is not null)
            .Select(static entry => entry!)
            .OrderBy(static entry => entry.AddedAtUtc)
            .ToArray();
    }

    public async Task<DialNumberEntry> AddAsync(
        string workspaceKey,
        string number,
        string? displayName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(number);

        var entry = new DialNumberEntry(
            NumberId: Guid.NewGuid().ToString("N"),
            Number: number.Trim(),
            DisplayName: string.IsNullOrWhiteSpace(displayName) ? null : displayName.Trim(),
            AddedAtUtc: DateTimeOffset.UtcNow);

        var json = JsonSerializer.Serialize(entry, JsonOptions);
        await dataStore
            .SetAsync(new PluginDataKey(DialerPlugin.Id, workspaceKey, Collection, entry.NumberId), json, cancellationToken)
            .ConfigureAwait(false);

        return entry;
    }

    public Task<bool> RemoveAsync(
        string workspaceKey,
        string numberId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(numberId))
        {
            return Task.FromResult(false);
        }

        return dataStore.RemoveAsync(
            new PluginDataKey(DialerPlugin.Id, workspaceKey, Collection, numberId.Trim()),
            cancellationToken);
    }

    private static DialNumberEntry? Deserialize(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<DialNumberEntry>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
