using Callora.Host.PluginContracts.Application.Data;

namespace Callora.Hosting.Application.Plugins;

/// <summary>
/// Enforces data ownership (PLAT-252): every key must carry the plugin id
/// the host assigned to the calling plugin — foreign ids are rejected, so
/// data isolation no longer relies on plugin good behavior.
/// </summary>
internal sealed class PluginBoundDataStore(IPluginDataStore inner, string ownPluginId) : IPluginDataStore
{
    public Task<string?> GetAsync(PluginDataKey key, CancellationToken cancellationToken = default)
    {
        EnsureOwned(key?.PluginId);
        return inner.GetAsync(key!, cancellationToken);
    }

    public Task SetAsync(PluginDataKey key, string jsonDocument, CancellationToken cancellationToken = default)
    {
        EnsureOwned(key?.PluginId);
        return inner.SetAsync(key!, jsonDocument, cancellationToken);
    }

    public Task<bool> RemoveAsync(PluginDataKey key, CancellationToken cancellationToken = default)
    {
        EnsureOwned(key?.PluginId);
        return inner.RemoveAsync(key!, cancellationToken);
    }

    public Task<IReadOnlyList<PluginDataEntry>> ListAsync(
        PluginDataCollectionKey collection,
        CancellationToken cancellationToken = default)
    {
        EnsureOwned(collection?.PluginId);
        return inner.ListAsync(collection!, cancellationToken);
    }

    public Task<IReadOnlyList<string>> ListWorkspaceKeysAsync(
        string pluginId,
        string collection,
        CancellationToken cancellationToken = default)
    {
        EnsureOwned(pluginId);
        return inner.ListWorkspaceKeysAsync(pluginId, collection, cancellationToken);
    }

    private void EnsureOwned(string? requestedPluginId)
    {
        if (!string.Equals(requestedPluginId?.Trim(), ownPluginId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Plugin '{ownPluginId}' addressed data of plugin '{requestedPluginId}'. " +
                "Plugin data is isolated per plugin id.");
        }
    }
}
