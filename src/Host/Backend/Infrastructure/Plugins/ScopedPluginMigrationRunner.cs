using Callora.Host.Backend.Application.Plugins;
using Callora.Host.Backend.Domain.Plugins;
using Callora.Host.Backend.Infrastructure.Persistence;
using Callora.Host.PluginContracts.Application.Migrations;
using Microsoft.EntityFrameworkCore;

namespace Callora.Host.Backend.Infrastructure.Plugins;

/// <summary>
/// Applies pending plugin migrations against the host database connection,
/// one transaction per migration, with bookkeeping in plugin_migrations.
/// Plugin tables must use the "plugin_&lt;pluginId&gt;_*" prefix (convention,
/// logged for audit).
/// </summary>
public sealed class ScopedPluginMigrationRunner(
    IServiceScopeFactory scopeFactory,
    ILogger<ScopedPluginMigrationRunner> logger) : IPluginMigrationRunner
{
    public async Task RunAsync(
        string pluginId,
        IReadOnlyList<IPluginMigration> migrations,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        if (migrations.Count == 0)
        {
            return;
        }

        var normalizedPluginId = pluginId.Trim();
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<HostPersistenceDbContext>();

        var appliedVersions = await dbContext.PluginMigrations
            .Where(record => record.PluginId == normalizedPluginId)
            .Select(record => record.Version)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);

        var pending = PluginMigrationPlanner.SelectPending(appliedVersions, migrations);
        if (pending.Count == 0)
        {
            return;
        }

        var connection = dbContext.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        }

        foreach (var migration in pending)
        {
            await using var transaction = await dbContext.Database
                .BeginTransactionAsync(cancellationToken)
                .ConfigureAwait(false);

            logger.LogInformation(
                "Applying plugin migration {PluginId} v{Version}: {Description}",
                normalizedPluginId,
                migration.Version,
                migration.Description);

            await migration.UpAsync(connection, cancellationToken).ConfigureAwait(false);

            dbContext.PluginMigrations.Add(new PluginMigrationRecord
            {
                Id = Guid.NewGuid(),
                PluginId = normalizedPluginId,
                Version = migration.Version,
                Description = migration.Description,
                AppliedAtUtc = DateTimeOffset.UtcNow
            });
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
