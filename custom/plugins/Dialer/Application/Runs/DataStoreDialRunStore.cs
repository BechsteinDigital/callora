using System.Text.Json;
using Callora.Host.PluginContracts.Application.Data;

namespace Callora.Plugins.Dialer.Application.Runs;

/// <summary>
/// Persists dial run snapshots in the host plugin data store so runs survive
/// restarts. Keeps one document per run plus a "latest" pointer per workspace.
/// </summary>
public sealed class DataStoreDialRunStore(IPluginDataStore dataStore)
{
    private const string Collection = "dial-runs";
    private const string LatestEntryKey = "latest";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task SaveAsync(DialRunSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var json = JsonSerializer.Serialize(snapshot, JsonOptions);
        await dataStore
            .SetAsync(BuildKey(snapshot.WorkspaceKey, snapshot.RunId), json, cancellationToken)
            .ConfigureAwait(false);
        await dataStore
            .SetAsync(BuildKey(snapshot.WorkspaceKey, LatestEntryKey), json, cancellationToken)
            .ConfigureAwait(false);
    }

    public Task<DialRunSnapshot?> GetLatestAsync(string workspaceKey, CancellationToken cancellationToken = default) =>
        GetAsync(workspaceKey, LatestEntryKey, cancellationToken);

    public Task<DialRunSnapshot?> GetRunAsync(string workspaceKey, string runId, CancellationToken cancellationToken = default) =>
        GetAsync(workspaceKey, runId, cancellationToken);

    private async Task<DialRunSnapshot?> GetAsync(
        string workspaceKey,
        string entryKey,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(workspaceKey) || string.IsNullOrWhiteSpace(entryKey))
            return null;

        var json = await dataStore
            .GetAsync(BuildKey(workspaceKey.Trim(), entryKey.Trim()), cancellationToken)
            .ConfigureAwait(false);
        if (json is null)
            return null;

        try
        {
            return JsonSerializer.Deserialize<DialRunSnapshot>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static PluginDataKey BuildKey(string workspaceKey, string entryKey) =>
        new(DialerPlugin.Id, workspaceKey, Collection, entryKey);
}
