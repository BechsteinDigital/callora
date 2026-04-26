using Callora.Host.Backend.Application.Abstractions;
using Callora.Host.Backend.Application.Abstractions.Extensions;
using Callora.Host.Backend.Application.Abstractions.Persistence;
using Callora.Host.Backend.Application.Abstractions.Security;
using Callora.Host.Backend.Application.Abstractions.Tenants;
using Callora.Host.Backend.Application.Abstractions.Workspaces;
using Callora.Host.Backend.Domain.Security;
using Callora.Host.Backend.Application.Policies;
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

        services.AddScoped<IPluginInstallationRepository, EfPluginInstallationRepository>();
        services.AddScoped<IPluginAuditLogRepository, EfPluginAuditLogRepository>();
        services.AddScoped<IHostUnitOfWork, EfHostUnitOfWork>();
        services.AddScoped<IBackendRbacStore, EfBackendRbacStore>();
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
