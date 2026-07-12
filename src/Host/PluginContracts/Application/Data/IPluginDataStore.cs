namespace Callora.Host.PluginContracts.Application.Data;

/// <summary>
/// Host-provided document storage for plugins, scoped by plugin, workspace and
/// collection. Payloads are JSON documents; plugins own their serialization.
/// Resolvable from <c>IHostPluginContext.Services</c>.
/// </summary>
public interface IPluginDataStore
{
    /// <summary>
    /// Returns one document, or null when it does not exist.
    /// </summary>
    Task<string?> GetAsync(PluginDataKey key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates or replaces one document.
    /// </summary>
    Task SetAsync(PluginDataKey key, string jsonDocument, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes one document. Returns false when it did not exist.
    /// </summary>
    Task<bool> RemoveAsync(PluginDataKey key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all documents of one collection ordered by entry key.
    /// </summary>
    Task<IReadOnlyList<PluginDataEntry>> ListAsync(
        PluginDataCollectionKey collection,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all workspace keys that contain documents of one collection,
    /// excluding plugin-global data.
    /// </summary>
    Task<IReadOnlyList<string>> ListWorkspaceKeysAsync(
        string pluginId,
        string collection,
        CancellationToken cancellationToken = default);
}
