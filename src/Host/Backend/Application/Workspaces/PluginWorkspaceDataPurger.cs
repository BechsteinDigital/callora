using Callora.Host.PluginContracts.Application.Persistence;
using Callora.Hosting.Application.Plugins;

namespace Callora.Host.Backend.Application.Workspaces;

/// <summary>
/// Invokes every plugin's exported <see cref="IWorkspaceDataPurgeContributor"/>
/// when a workspace is purged, so each plugin deletes its own workspace-scoped
/// data in its dedicated schema (<c>plugin_&lt;id&gt;</c>, PLAT-260) that the host
/// cannot reach (REV2 §14). Best-effort per contributor: one failure is logged
/// and blocks neither the other contributors nor the committed host purge — an
/// orphaned plugin row is a compliance retry, not a purge blocker.
/// </summary>
public sealed class PluginWorkspaceDataPurger(
    ICalloraPluginCatalog catalog,
    ILogger<PluginWorkspaceDataPurger> logger)
{
    public async Task PurgeAsync(string workspaceKey, CancellationToken cancellationToken = default)
    {
        foreach (var contributor in catalog.GetExports<IWorkspaceDataPurgeContributor>())
        {
            try
            {
                await contributor.PurgeWorkspaceAsync(workspaceKey, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                logger.LogWarning(
                    exception,
                    "Plugin workspace-data purge contributor {Contributor} failed for workspace {WorkspaceKey}; " +
                    "plugin-owned rows may remain and need a retry.",
                    contributor.GetType().FullName,
                    workspaceKey);
            }
        }
    }
}
