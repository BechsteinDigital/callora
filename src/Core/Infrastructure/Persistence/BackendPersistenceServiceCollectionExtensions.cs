using Callora.Core.Application.Audit;
using Callora.Core.Application.Extensions;
using Callora.Core.Application.Integrations;
using Callora.Core.Application.Persistence;
using Callora.Core.Application.Policies;
using Callora.Core.Application.Security;
using Callora.Core.Application.Tenants;
using Callora.Core.Application.Workspaces;
using Callora.Core.Domain.Security;
using Callora.Core.Infrastructure.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Callora.Core.Infrastructure.Persistence;

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
        // Session revocation (#105): a durable revocation list, the bounded
        // account-state cache the request-path validator reads, and the decorator
        // that drops a cached account the moment its stamp rotates.
        services.AddScoped<EfBackendUserStore>();
        services.AddScoped<IBackendUserStore>(provider => new SessionStateInvalidatingUserStore(
            provider.GetRequiredService<EfBackendUserStore>(),
            provider.GetRequiredService<BackendSessionStateCache>()));
        services.AddScoped<IBackendSessionRevocationStore, EfBackendSessionRevocationStore>();
        services.AddSingleton<BackendSessionStateCache>();
        services.AddScoped<IBackendSessionValidator, BackendSessionValidator>();
        services.AddScoped<ITenantManagementStore, EfTenantManagementStore>();
        services.AddScoped<IWorkspaceManagementStore, EfWorkspaceManagementStore>();
        services.AddScoped<IWorkspaceSurfaceStore, EfWorkspaceSurfaceStore>();
        services.AddScoped<Callora.Core.Application.Surfaces.ISurfaceSessionStore, EfSurfaceSessionStore>();
        services.AddScoped<Callora.Core.Application.Surfaces.ISurfaceHandoffTicketStore, EfSurfaceHandoffTicketStore>();
        // Registered as the concrete type only: plugins reach resume tickets from outside any request
        // scope, so the contract itself is served by the singleton facade in host composition.
        services.AddScoped<EfSessionResumeTicketStore>();
        services.AddScoped<WorkspaceSurfaceProvisioner>();
        services.AddSingleton<
            Callora.Core.Application.Workspaces.Contracts.IWorkspaceSurfaceProvisioner,
            ScopedWorkspaceSurfaceProvisioner>();
        services.AddScoped<IWorkspaceTemplateRegistryStore, EfWorkspaceTemplateRegistryStore>();
        services.AddScoped<IWorkspaceThemeSettingsStore, EfWorkspaceThemeSettingsStore>();
        services.AddScoped<IWorkspaceSectionLayoutStore, EfWorkspaceSectionLayoutStore>();
        services.AddScoped<IPasswordHasher<BackendUser>, PasswordHasher<BackendUser>>();

        services.AddScoped<IHostAuditStore, DatabaseHostAuditStore>();
        services.AddScoped<BackendRbacDatabaseSeeder>();
        services.AddHostedService<HostDatabaseInitializationHostedService>();

        return services;
    }
}
