using Callora.Host.PluginContracts.Application.Persistence;
using Callora.Core.Application.Plugins;

namespace Callora.Core.Application.Workspaces;

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
    /// <summary>
    /// Invokes every exported contributor for the workspace and returns how many
    /// failed. A non-zero result is a compliance-relevant partial purge: those
    /// plugins' rows may remain and the failure must be retried.
    /// </summary>
    public async Task<int> PurgeAsync(string workspaceKey, CancellationToken cancellationToken = default)
    {
        var failures = 0;
        foreach (var contributor in catalog.GetExports<IWorkspaceDataPurgeContributor>())
        {
            try
            {
                await contributor.PurgeWorkspaceAsync(workspaceKey, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                failures++;
                // A failed erasure is compliance-critical, not a warning: it must
                // surface to alerting so the orphaned rows get retried.
                logger.LogError(
                    exception,
                    "COMPLIANCE: plugin workspace-data purge contributor {Contributor} failed for workspace " +
                    "{WorkspaceKey}; plugin-owned rows remain and must be retried.",
                    contributor.GetType().FullName,
                    workspaceKey);
            }
        }

        return failures;
    }
}
