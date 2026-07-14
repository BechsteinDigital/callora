using Callora.Host.Backend.Application.Audit;
using Callora.Host.Backend.Application.Extensions;
using Callora.Host.Backend.Application.Integrations;
using Callora.Host.Backend.Application.Persistence;
using Callora.Host.Backend.Application.Security;
using Callora.Host.Backend.Application.Tenants;
using Callora.Host.Backend.Application.Workspaces;
using Callora.Host.Backend.Domain.Security;
using Callora.Host.Backend.Application.Policies;
using Callora.Host.Backend.Infrastructure.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Callora.Host.Backend.Infrastructure.Persistence;

public static class BackendPersistenceServiceCollectionExtensions
{
    public static IServiceCollection AddBackendPersistence(
        this IServiceCollection services,
        BackendHostOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(options.DatabaseConnectionString);

        services.AddDbContext<HostPersistenceDbContext>(db =>
            db.UseNpgsql(options.DatabaseConnectionString));

        // Trägt den Workspace-Scope des Requests in den globalen Query-Filter
        // des DbContext (PLAT-267). Operatoren/Nicht-Requests umgehen ihn.
        services.AddHttpContextAccessor();
        services.AddScoped<IWorkspaceScopeContext, HttpWorkspaceScopeContext>();

        services.AddScoped<IPluginInstallationRepository, EfPluginInstallationRepository>();
        services.AddScoped<IPluginAuditLogRepository, EfPluginAuditLogRepository>();
        services.AddScoped<IHostUnitOfWork, EfHostUnitOfWork>();
        services.AddScoped<IBackendRbacStore, EfBackendRbacStore>();
        services.AddScoped<IIntegrationCredentialStore, EfIntegrationCredentialStore>();
        services.AddScoped<IBackendUserStore, EfBackendUserStore>();
        services.AddScoped<ITenantManagementStore, EfTenantManagementStore>();
        services.AddScoped<IWorkspaceManagementStore, EfWorkspaceManagementStore>();
        services.AddScoped<IWorkspaceTemplateRegistryStore, EfWorkspaceTemplateRegistryStore>();
        services.AddScoped<IWorkspaceThemeSettingsStore, EfWorkspaceThemeSettingsStore>();
        services.AddScoped<IPasswordHasher<BackendUser>, PasswordHasher<BackendUser>>();

        services.AddScoped<IHostAuditStore, DatabaseHostAuditStore>();
        services.AddScoped<BackendRbacDatabaseSeeder>();
        services.AddHostedService<HostDatabaseInitializationHostedService>();

        return services;
    }
}
