namespace Callora.Core.Application.Persistence;

/// <summary>
/// Removes host-owned plugin data documents for one plugin. These rows live in
/// the host schema (keyed by plugin id), so a plugin's dedicated-schema drop
/// does not cover them; they must be purged separately on uninstall.
/// </summary>
public interface IPluginDataDocumentCleaner
{
    /// <summary>Deletes all documents owned by the plugin; returns the row count.</summary>
    Task<int> DeleteByPluginIdAsync(string pluginId, CancellationToken cancellationToken = default);
}
