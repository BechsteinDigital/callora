using Callora.Core.Extensibility;

namespace Callora.Core.Application.Persistence.Contracts;

/// <summary>
/// A plugin implements this to erase its own workspace-scoped data when a
/// workspace is purged (GDPR cascading deletion, REV2 §14). The host cannot
/// reach a plugin's dedicated schema (<c>plugin_&lt;id&gt;</c>, PLAT-260), so each
/// plugin that stores workspace data exports one contributor via
/// <c>IHostPluginContext.Export</c>; the host purge invokes them all.
/// </summary>
[CalloraExtensible("Extension point — implement to purge plugin workspace data (REV2 §8.2)")]
public interface IWorkspaceDataPurgeContributor
{
    /// <summary>
    /// Deletes all data the plugin holds for the given workspace. Invoked after
    /// the host purge has committed; a failure is logged, not fatal.
    /// </summary>
    Task PurgeWorkspaceAsync(string workspaceKey, CancellationToken cancellationToken = default);
}
