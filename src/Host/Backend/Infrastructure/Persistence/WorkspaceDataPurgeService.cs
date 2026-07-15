using Callora.Host.Backend.Application.Media;
using Callora.Host.Backend.Application.Workspaces;
using Microsoft.EntityFrameworkCore;

namespace Callora.Host.Backend.Infrastructure.Persistence;

/// <summary>
/// Deletes a workspace together with all workspace-bound data in one
/// transaction (GDPR cascading deletion, PLAT-242): jobs, notifications,
/// flows, webhooks, plugin data, activations, theme values, config values,
/// custom fields, media rows and memberships. Media blobs are removed
/// best-effort after the commit.
/// </summary>
public sealed class WorkspaceDataPurgeService(
    HostPersistenceDbContext dbContext,
    IMediaStorage mediaStorage,
    PluginWorkspaceDataPurger pluginPurger,
    ILogger<WorkspaceDataPurgeService> logger) : IWorkspaceDataPurgeService
{
    public async Task<bool> PurgeAsync(string workspaceKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(workspaceKey))
        {
            return false;
        }

        var key = workspaceKey.Trim();
        var workspace = await dbContext.Workspaces
            .SingleOrDefaultAsync(x => x.WorkspaceKey == key, cancellationToken)
            .ConfigureAwait(false);
        if (workspace is null)
        {
            return false;
        }

        var mediaIds = await dbContext.MediaItems
            .Where(x => x.WorkspaceKey == key)
            .Select(x => x.Id)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);

        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        var deletedRows = 0;
        deletedRows += await dbContext.BackgroundJobs
            .Where(x => x.WorkspaceKey == key)
            .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        deletedRows += await dbContext.Notifications
            .Where(x => x.WorkspaceKey == key)
            .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        deletedRows += await dbContext.Flows
            .Where(x => x.WorkspaceKey == key)
            .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        deletedRows += await dbContext.WebhookSubscriptions
            .Where(x => x.WorkspaceKey == key)
            .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        deletedRows += await dbContext.PluginDataDocuments
            .Where(x => x.WorkspaceKey == key)
            .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        deletedRows += await dbContext.WorkspacePluginActivations
            .Where(x => x.WorkspaceKey == key)
            .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        deletedRows += await dbContext.WorkspaceThemeSettingValues
            .Where(x => x.WorkspaceKey == key)
            .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        deletedRows += await dbContext.SystemConfigValues
            .Where(x => x.Scope == "workspace" && x.ScopeKey == key)
            .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        deletedRows += await dbContext.CustomFieldValues
            .Where(x => x.WorkspaceKey == key ||
                        (x.EntityName == "workspace" && x.EntityId == key))
            .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        deletedRows += await dbContext.MediaItems
            .Where(x => x.WorkspaceKey == key)
            .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        deletedRows += await dbContext.WorkspaceMemberships
            .Where(x => x.WorkspaceId == workspace.Id)
            .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);

        dbContext.Workspaces.Remove(workspace);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation(
            "Purged workspace {WorkspaceKey}: {DeletedRows} dependent rows and {MediaCount} media assets removed.",
            key,
            deletedRows,
            mediaIds.Length);

        // Plugins own data the host cannot reach (plugin_<id> schemas, PLAT-260):
        // ask each to erase its workspace data after the host purge committed.
        var pluginPurgeFailures = await pluginPurger.PurgeAsync(key, cancellationToken).ConfigureAwait(false);
        if (pluginPurgeFailures > 0)
        {
            logger.LogError(
                "COMPLIANCE: workspace {WorkspaceKey} purge left {FailedContributors} plugin contributor(s) " +
                "unpurged; plugin-owned rows may remain and must be retried.",
                key,
                pluginPurgeFailures);
        }

        foreach (var mediaId in mediaIds)
        {
            try
            {
                await mediaStorage.DeleteAsync(mediaId, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                // Die DB-Löschung ist committed; verwaiste Blobs sind ein
                // Storage-Hygiene-Problem, kein Datenschutz-Blocker.
                logger.LogWarning(exception, "Media blob {MediaId} could not be deleted during purge.", mediaId);
            }
        }

        return true;
    }
}
