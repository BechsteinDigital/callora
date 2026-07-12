namespace Callora.Host.PluginContracts.Application.Data;

/// <summary>
/// Address of one plugin data document.
/// </summary>
/// <param name="PluginId">Owning plugin identifier.</param>
/// <param name="WorkspaceKey">Workspace scope, or null for plugin-global data.</param>
/// <param name="Collection">Logical collection name, for example "sip-accounts".</param>
/// <param name="EntryKey">Entry identifier unique within the collection.</param>
public sealed record PluginDataKey(
    string PluginId,
    string? WorkspaceKey,
    string Collection,
    string EntryKey);
