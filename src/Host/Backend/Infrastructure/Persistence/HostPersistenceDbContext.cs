using Callora.Host.Backend.Domain.Audit;
using Callora.Host.Backend.Domain.Plugins;
using Callora.Host.Backend.Domain.Security;
using Callora.Host.Backend.Domain.Tenants;
using Callora.Host.Backend.Domain.Workspaces;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using WorkspaceEntity = Callora.Host.Backend.Domain.Workspaces.Workspace;

namespace Callora.Host.Backend.Infrastructure.Persistence;

public sealed class HostPersistenceDbContext(
    DbContextOptions<HostPersistenceDbContext> options,
    Callora.Host.Backend.Application.Security.IWorkspaceScopeContext? workspaceScope = null)
    : DbContext(options), IDataProtectionKeyContext
{
    // Datenbank-Keyring statt Dateisystem: mehrinstanzfähig (PLAT-232).
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    public DbSet<PluginInstallation> PluginInstallations => Set<PluginInstallation>();

    public DbSet<WorkspacePluginActivation> WorkspacePluginActivations => Set<WorkspacePluginActivation>();

    public DbSet<PluginAuditLog> PluginAuditLogs => Set<PluginAuditLog>();

    public DbSet<PluginDataDocument> PluginDataDocuments => Set<PluginDataDocument>();

    public DbSet<Callora.Host.Backend.Domain.Jobs.BackgroundJob> BackgroundJobs =>
        Set<Callora.Host.Backend.Domain.Jobs.BackgroundJob>();

    public DbSet<Callora.Host.Backend.Domain.Entitlements.MarketplaceEntitlementEventRecord> MarketplaceEntitlementEvents =>
        Set<Callora.Host.Backend.Domain.Entitlements.MarketplaceEntitlementEventRecord>();

    public DbSet<BackendRbacRole> BackendRbacRoles => Set<BackendRbacRole>();

    public DbSet<BackendRbacRoleGrant> BackendRbacRoleGrants => Set<BackendRbacRoleGrant>();

    public DbSet<BackendRbacUserRole> BackendRbacUserRoles => Set<BackendRbacUserRole>();

    public DbSet<BackendUser> BackendUsers => Set<BackendUser>();

    public DbSet<Tenant> Tenants => Set<Tenant>();

    public DbSet<WorkspaceEntity> Workspaces => Set<WorkspaceEntity>();

    public DbSet<WorkspaceMembership> WorkspaceMemberships => Set<WorkspaceMembership>();

    public DbSet<Callora.Host.Backend.Domain.Extensions.WorkspaceTemplateDefinition> WorkspaceTemplateDefinitions =>
        Set<Callora.Host.Backend.Domain.Extensions.WorkspaceTemplateDefinition>();

    public DbSet<Callora.Host.Backend.Domain.Extensions.WorkspaceThemeSettingDefinition> WorkspaceThemeSettingDefinitions =>
        Set<Callora.Host.Backend.Domain.Extensions.WorkspaceThemeSettingDefinition>();

    public DbSet<Callora.Host.Backend.Domain.Extensions.WorkspaceThemeSettingValue> WorkspaceThemeSettingValues =>
        Set<Callora.Host.Backend.Domain.Extensions.WorkspaceThemeSettingValue>();

    public DbSet<Callora.Host.Backend.Domain.Configuration.SystemConfigDefinition> SystemConfigDefinitions =>
        Set<Callora.Host.Backend.Domain.Configuration.SystemConfigDefinition>();

    public DbSet<Callora.Host.Backend.Domain.Configuration.SystemConfigValue> SystemConfigValues =>
        Set<Callora.Host.Backend.Domain.Configuration.SystemConfigValue>();

    public DbSet<Callora.Host.Backend.Domain.Webhooks.WebhookSubscription> WebhookSubscriptions =>
        Set<Callora.Host.Backend.Domain.Webhooks.WebhookSubscription>();

    public DbSet<Callora.Host.Backend.Domain.Notifications.NotificationEntry> Notifications =>
        Set<Callora.Host.Backend.Domain.Notifications.NotificationEntry>();

    public DbSet<Callora.Host.Backend.Domain.Media.MediaItem> MediaItems =>
        Set<Callora.Host.Backend.Domain.Media.MediaItem>();

    public DbSet<Callora.Host.Backend.Domain.Plugins.PluginMigrationRecord> PluginMigrations =>
        Set<Callora.Host.Backend.Domain.Plugins.PluginMigrationRecord>();

    public DbSet<Callora.Host.Backend.Domain.CustomFields.CustomFieldDefinition> CustomFieldDefinitions =>
        Set<Callora.Host.Backend.Domain.CustomFields.CustomFieldDefinition>();

    public DbSet<Callora.Host.Backend.Domain.CustomFields.CustomFieldValue> CustomFieldValues =>
        Set<Callora.Host.Backend.Domain.CustomFields.CustomFieldValue>();

    public DbSet<Callora.Host.Backend.Domain.Flows.FlowDefinition> Flows =>
        Set<Callora.Host.Backend.Domain.Flows.FlowDefinition>();

    public DbSet<Callora.Host.Backend.Domain.Entitlements.PluginEntitlement> PluginEntitlements =>
        Set<Callora.Host.Backend.Domain.Entitlements.PluginEntitlement>();

    public DbSet<Callora.Host.Backend.Domain.Integrations.IntegrationCredential> IntegrationCredentials =>
        Set<Callora.Host.Backend.Domain.Integrations.IntegrationCredential>();

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

        modelBuilder.Entity<Callora.Host.Backend.Domain.Media.MediaItem>()
            .HasQueryFilter(e => !WorkspaceFilterActive || e.WorkspaceKey == WorkspaceFilterKey);
        modelBuilder.Entity<Callora.Host.Backend.Domain.Flows.FlowDefinition>()
            .HasQueryFilter(e => !WorkspaceFilterActive || e.WorkspaceKey == WorkspaceFilterKey);
        modelBuilder.Entity<Callora.Host.Backend.Domain.Extensions.WorkspaceThemeSettingValue>()
            .HasQueryFilter(e => !WorkspaceFilterActive || e.WorkspaceKey == WorkspaceFilterKey);
        modelBuilder.Entity<Callora.Host.Backend.Domain.Plugins.WorkspacePluginActivation>()
            .HasQueryFilter(e => !WorkspaceFilterActive || e.WorkspaceKey == WorkspaceFilterKey);
        modelBuilder.Entity<Callora.Host.Backend.Domain.Plugins.PluginDataDocument>()
            .HasQueryFilter(e => !WorkspaceFilterActive || e.WorkspaceKey == WorkspaceFilterKey);
    }
}
