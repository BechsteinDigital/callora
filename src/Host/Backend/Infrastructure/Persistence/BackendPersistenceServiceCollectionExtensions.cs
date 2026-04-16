using Callora.Host.Backend.Application.Abstractions;
using Callora.Host.Backend.Application.Abstractions.Persistence;
using Callora.Host.Backend.Application.Policies;
using Microsoft.EntityFrameworkCore;

namespace Callora.Host.Backend.Infrastructure.Persistence;

public static class BackendPersistenceServiceCollectionExtensions
{
    public static IServiceCollection AddBackendPersistence(
        this IServiceCollection services,
        BackendHostOptions options)
    {
        services.AddDbContext<HostPersistenceDbContext>(db =>
            db.UseSqlite(BuildConnectionString(options.DatabasePath)));

        services.AddScoped<IPluginInstallationRepository, EfPluginInstallationRepository>();
        services.AddScoped<IPluginAuditLogRepository, EfPluginAuditLogRepository>();
        services.AddScoped<IHostUnitOfWork, EfHostUnitOfWork>();

        services.AddScoped<IHostAuditStore, DatabaseHostAuditStore>();
        services.AddHostedService<HostDatabaseInitializationHostedService>();

        return services;
    }

    private static string BuildConnectionString(string databasePath)
    {
        var path = string.IsNullOrWhiteSpace(databasePath)
            ? Path.Combine(AppContext.BaseDirectory, "plugins", "host.db")
            : databasePath;

        var fullPath = Path.IsPathRooted(path)
            ? path
            : Path.Combine(AppContext.BaseDirectory, path);

        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        return $"Data Source={fullPath}";
    }
}
