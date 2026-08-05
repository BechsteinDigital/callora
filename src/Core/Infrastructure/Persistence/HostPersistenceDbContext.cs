using Callora.Core.Domain.Audit;
using Callora.Core.Domain.Plugins;
using Callora.Core.Domain.Security;
using Callora.Core.Domain.Tenants;
using Callora.Core.Domain.Workspaces;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using WorkspaceEntity = Callora.Core.Domain.Workspaces.Workspace;

namespace Callora.Core.Infrastructure.Persistence;

public sealed class HostPersistenceDbContext(
    DbContextOptions<HostPersistenceDbContext> options,
    Callora.Core.Application.Security.IWorkspaceScopeContext? workspaceScope = null)
    : DbContext(options), IDataProtectionKeyContext
{
    // Datenbank-Keyring statt Dateisystem: mehrinstanzfähig (PLAT-232).
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    public DbSet<PluginInstallation> PluginInstallations => Set<PluginInstallation>();

    public DbSet<WorkspacePluginActivation> WorkspacePluginActivations => Set<WorkspacePluginActivation>();

    public DbSet<PluginAuditLog> PluginAuditLogs => Set<PluginAuditLog>();

    public DbSet<PluginDataDocument> PluginDataDocuments => Set<PluginDataDocument>();

    public DbSet<Callora.Core.Domain.Jobs.BackgroundJob> BackgroundJobs =>
        Set<Callora.Core.Domain.Jobs.BackgroundJob>();

    public DbSet<Callora.Core.Domain.Entitlements.MarketplaceEntitlementEventRecord> MarketplaceEntitlementEvents =>
        Set<Callora.Core.Domain.Entitlements.MarketplaceEntitlementEventRecord>();

    public DbSet<BackendRbacRole> BackendRbacRoles => Set<BackendRbacRole>();

    public DbSet<BackendRbacRoleGrant> BackendRbacRoleGrants => Set<BackendRbacRoleGrant>();

    public DbSet<BackendRbacUserRole> BackendRbacUserRoles => Set<BackendRbacUserRole>();

    public DbSet<BackendUser> BackendUsers => Set<BackendUser>();

    public DbSet<BackendRevokedSession> BackendRevokedSessions => Set<BackendRevokedSession>();

    public DbSet<Tenant> Tenants => Set<Tenant>();

    public DbSet<WorkspaceEntity> Workspaces => Set<WorkspaceEntity>();

    public DbSet<WorkspaceSurface> WorkspaceSurfaces => Set<WorkspaceSurface>();

    public DbSet<WorkspaceMembership> WorkspaceMemberships => Set<WorkspaceMembership>();

    public DbSet<Callora.Core.Domain.Extensions.WorkspaceTemplateDefinition> WorkspaceTemplateDefinitions =>
        Set<Callora.Core.Domain.Extensions.WorkspaceTemplateDefinition>();

    public DbSet<Callora.Core.Domain.Extensions.WorkspaceThemeSettingDefinition> WorkspaceThemeSettingDefinitions =>
        Set<Callora.Core.Domain.Extensions.WorkspaceThemeSettingDefinition>();

    public DbSet<Callora.Core.Domain.Extensions.WorkspaceThemeSettingValue> WorkspaceThemeSettingValues =>
        Set<Callora.Core.Domain.Extensions.WorkspaceThemeSettingValue>();

    public DbSet<Callora.Core.Domain.Configuration.SystemConfigDefinition> SystemConfigDefinitions =>
        Set<Callora.Core.Domain.Configuration.SystemConfigDefinition>();

    public DbSet<Callora.Core.Domain.Configuration.SystemConfigValue> SystemConfigValues =>
        Set<Callora.Core.Domain.Configuration.SystemConfigValue>();

    public DbSet<Callora.Core.Domain.Webhooks.WebhookSubscription> WebhookSubscriptions =>
        Set<Callora.Core.Domain.Webhooks.WebhookSubscription>();

    public DbSet<Callora.Core.Domain.Notifications.NotificationEntry> Notifications =>
        Set<Callora.Core.Domain.Notifications.NotificationEntry>();

    public DbSet<Callora.Core.Domain.Media.MediaItem> MediaItems =>
        Set<Callora.Core.Domain.Media.MediaItem>();

    public DbSet<Callora.Core.Domain.Plugins.PluginMigrationRecord> PluginMigrations =>
        Set<Callora.Core.Domain.Plugins.PluginMigrationRecord>();

    public DbSet<Callora.Core.Domain.CustomFields.CustomFieldDefinition> CustomFieldDefinitions =>
        Set<Callora.Core.Domain.CustomFields.CustomFieldDefinition>();

    public DbSet<Callora.Core.Domain.CustomFields.CustomFieldValue> CustomFieldValues =>
        Set<Callora.Core.Domain.CustomFields.CustomFieldValue>();

    public DbSet<Callora.Core.Domain.Flows.FlowDefinition> Flows =>
        Set<Callora.Core.Domain.Flows.FlowDefinition>();

    public DbSet<Callora.Core.Domain.Entitlements.PluginEntitlement> PluginEntitlements =>
        Set<Callora.Core.Domain.Entitlements.PluginEntitlement>();

    public DbSet<Callora.Core.Domain.Integrations.IntegrationCredential> IntegrationCredentials =>
        Set<Callora.Core.Domain.Integrations.IntegrationCredential>();

    public DbSet<Callora.Core.Domain.Surfaces.SurfaceSessionRecord> SurfaceSessions =>
        Set<Callora.Core.Domain.Surfaces.SurfaceSessionRecord>();

    public DbSet<Callora.Core.Domain.Surfaces.SurfaceHandoffTicketRecord> SurfaceHandoffTickets =>
        Set<Callora.Core.Domain.Surfaces.SurfaceHandoffTicketRecord>();

    // Workspace-Isolation als Backstop (PLAT-267): ein workspace-gebundener
    // Aufrufer liest nur Zeilen seines Workspace, auch wenn ein Store das
    // explizite Where vergisst. Operatoren und Nicht-Request-Kontexte (Jobs,
    // Seeding, Migrationen) haben keinen Scope -> Filter inaktiv.
    private bool WorkspaceFilterActive => workspaceScope?.IsWorkspaceScoped == true;

    private string WorkspaceFilterKey => workspaceScope?.WorkspaceKey ?? string.Empty;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(HostPersistenceDbContext).Assembly);

        // Strikt workspace-gebundene Entities (WorkspaceKey nicht nullable):
        // ein scoped Aufrufer sieht ausschließlich Zeilen seines Workspace.
        modelBuilder.Entity<Callora.Core.Domain.Media.MediaItem>()
            .HasQueryFilter(e => !WorkspaceFilterActive || e.WorkspaceKey == WorkspaceFilterKey);
        modelBuilder.Entity<Callora.Core.Domain.Flows.FlowDefinition>()
            .HasQueryFilter(e => !WorkspaceFilterActive || e.WorkspaceKey == WorkspaceFilterKey);
        modelBuilder.Entity<Callora.Core.Domain.Extensions.WorkspaceThemeSettingValue>()
            .HasQueryFilter(e => !WorkspaceFilterActive || e.WorkspaceKey == WorkspaceFilterKey);
        modelBuilder.Entity<Callora.Core.Domain.Plugins.WorkspacePluginActivation>()
            .HasQueryFilter(e => !WorkspaceFilterActive || e.WorkspaceKey == WorkspaceFilterKey);
        modelBuilder.Entity<Callora.Core.Domain.Plugins.PluginDataDocument>()
            .HasQueryFilter(e => !WorkspaceFilterActive || e.WorkspaceKey == WorkspaceFilterKey);

        // Entities mit nullable WorkspaceKey: null bedeutet plattformweite Zeile
        // (z. B. globale Notifications). Ein scoped Aufrufer sieht seine Zeilen
        // plus die globalen, aber keine fremden Workspaces (PLAT-267).
        modelBuilder.Entity<Callora.Core.Domain.Notifications.NotificationEntry>()
            .HasQueryFilter(e => !WorkspaceFilterActive || e.WorkspaceKey == null || e.WorkspaceKey == WorkspaceFilterKey);
        modelBuilder.Entity<Callora.Core.Domain.Webhooks.WebhookSubscription>()
            .HasQueryFilter(e => !WorkspaceFilterActive || e.WorkspaceKey == null || e.WorkspaceKey == WorkspaceFilterKey);
        modelBuilder.Entity<Callora.Core.Domain.Integrations.IntegrationCredential>()
            .HasQueryFilter(e => !WorkspaceFilterActive || e.WorkspaceKey == null || e.WorkspaceKey == WorkspaceFilterKey);
        modelBuilder.Entity<Callora.Core.Domain.CustomFields.CustomFieldValue>()
            .HasQueryFilter(e => !WorkspaceFilterActive || e.WorkspaceKey == null || e.WorkspaceKey == WorkspaceFilterKey);
        modelBuilder.Entity<Callora.Core.Domain.Entitlements.PluginEntitlement>()
            .HasQueryFilter(e => !WorkspaceFilterActive || e.WorkspaceKey == null || e.WorkspaceKey == WorkspaceFilterKey);
        modelBuilder.Entity<Callora.Core.Domain.Entitlements.MarketplaceEntitlementEventRecord>()
            .HasQueryFilter(e => !WorkspaceFilterActive || e.WorkspaceKey == null || e.WorkspaceKey == WorkspaceFilterKey);
        modelBuilder.Entity<Callora.Core.Domain.Jobs.BackgroundJob>()
            .HasQueryFilter(e => !WorkspaceFilterActive || e.WorkspaceKey == null || e.WorkspaceKey == WorkspaceFilterKey);
    }

    /// <inheritdoc />
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        EnforceWorkspaceWriteScope();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    /// <inheritdoc />
    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        EnforceWorkspaceWriteScope();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    // Write-Backstop (PLAT-267): ein workspace-gebundener Aufrufer darf nur
    // Zeilen seines eigenen Workspace schreiben — auch wenn ein Store den
    // WorkspaceKey aus Client-Eingabe übernimmt. Operatoren/Jobs/Seeding haben
    // keinen Scope -> Enforcement inaktiv. Greift nur für Change-Tracker-Writes,
    // nicht für set-basierte ExecuteUpdate/ExecuteDelete (die laufen in
    // System-Kontexten ohne Scope).
    private void EnforceWorkspaceWriteScope()
    {
        if (!WorkspaceFilterActive)
        {
            return;
        }

        var scopedKey = WorkspaceFilterKey;
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State is not (Microsoft.EntityFrameworkCore.EntityState.Added or Microsoft.EntityFrameworkCore.EntityState.Modified))
            {
                continue;
            }

            var workspaceProperty = entry.Metadata.FindProperty("WorkspaceKey");
            if (workspaceProperty is null)
            {
                continue;
            }

            var value = entry.CurrentValues[workspaceProperty] as string;
            if (!string.Equals(value, scopedKey, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Workspace-scoped write to '{entry.Metadata.DisplayName()}' targets workspace " +
                    $"'{value ?? "<global>"}' but the caller is scoped to '{scopedKey}'.");
            }
        }
    }
}
