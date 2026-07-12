namespace Callora.Host.PluginContracts.Application.Data;

/// <summary>
/// Address of one plugin data collection.
/// </summary>
/// <param name="PluginId">Owning plugin identifier.</param>
/// <param name="WorkspaceKey">Workspace scope, or null for plugin-global data.</param>
/// <param name="Collection">Logical collection name, for example "sip-accounts".</param>
public sealed record PluginDataCollectionKey(
    string PluginId,
    string? WorkspaceKey,
    string Collection);
