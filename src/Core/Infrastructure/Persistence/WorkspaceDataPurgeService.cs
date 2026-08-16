using Callora.Core.Application.Media;
using Callora.Core.Application.Workspaces;
using Microsoft.EntityFrameworkCore;

namespace Callora.Core.Infrastructure.Persistence;

/// <summary>
/// Deletes a workspace together with all workspace-bound data in one
/// transaction (GDPR cascading deletion, PLAT-242): jobs, notifications,
/// flows, webhooks, plugin data, activations, theme values, config values,
/// custom fields, media rows, memberships, surface sessions and tickets, and
/// workspace-bound integration credentials. Media blobs are removed
/// best-effort after the commit.
/// <para>
/// <b>Vollständigkeit wird geprüft, nicht erinnert.</b> Diese Liste war einmal unvollständig, und
/// zwar nicht durch einen Fehler: Vier Tabellen kamen später dazu, jede mit einer
/// <c>workspace_key</c>-Spalte und ohne kaskadierenden Fremdschlüssel. Wer hier eine Tabelle
/// ergänzt oder eine neue workspace-gebundene anlegt, wird von
/// <c>WorkspacePurgeReachesEveryWorkspaceBoundTableTests</c> daran erinnert.
/// </para>
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
        // Wie die Konfiguration über Scope/ScopeKey gebunden, nicht über eine WorkspaceKey-Spalte
        // (ADR-024). Blieben sie stehen, erbte ein gleichnamiger neuer Workspace die Texte des
        // gelöschten — derselbe Befund, den dieser Dienst für vier andere Tabellen schon einmal
        // hatte.
        deletedRows += await dbContext.SnippetOverrides
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

        // Besucherdaten der Flächen. Kein Fremdschlüssel kaskadiert sie (die Migration legt nur
        // einen PK an), und der SurfaceSessionPurgeJobHandler räumt erst nach ExpiresAtUtc ab —
        // bei einem Gast-Kontext 30 Tage später. Bis dahin stünden subject_id, display_name und
        // claims_json eines gelöschten Workspaces weiter in der Datenbank.
        deletedRows += await dbContext.SurfaceSessions
            .Where(x => x.WorkspaceKey == key)
            .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        deletedRows += await dbContext.SurfaceHandoffTickets
            .Where(x => x.WorkspaceKey == key)
            .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        deletedRows += await dbContext.SessionResumeTickets
            .Where(x => x.WorkspaceKey == key)
            .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);

        // Workspace-gebundene Integrations-Schlüssel bleiben sonst AKTIV: Die Suche beim
        // Authentifizieren filtert nur auf IsActive und RevokedAtUtc, nie auf den Workspace, und
        // der Schlüssel trägt seinen workspace_key als Claim weiter. Da der Unique-Index auf
        // workspaces.WorkspaceKey nach der Löschung wieder frei ist, greift ein alter Schlüssel
        // auf die Daten eines gleichnamigen neuen Workspaces zu.
        deletedRows += await dbContext.IntegrationCredentials
            .Where(x => x.WorkspaceKey == key)
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
