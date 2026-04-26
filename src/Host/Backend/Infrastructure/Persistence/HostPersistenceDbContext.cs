using Callora.Host.Backend.Domain.Audit;
using Callora.Host.Backend.Domain.Plugins;
using Callora.Host.Backend.Domain.Security;
using Callora.Host.Backend.Domain.Tenants;
using Callora.Host.Backend.Domain.Workspaces;
using Microsoft.EntityFrameworkCore;
using WorkspaceEntity = Callora.Host.Backend.Domain.Workspaces.Workspace;

namespace Callora.Host.Backend.Infrastructure.Persistence;

public sealed class HostPersistenceDbContext(DbContextOptions<HostPersistenceDbContext> options) : DbContext(options)
{
    public DbSet<PluginInstallation> PluginInstallations => Set<PluginInstallation>();

    public DbSet<WorkspacePluginActivation> WorkspacePluginActivations => Set<WorkspacePluginActivation>();

    public DbSet<PluginAuditLog> PluginAuditLogs => Set<PluginAuditLog>();

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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(HostPersistenceDbContext).Assembly);
    }
}
