using Callora.Host.Backend.Application.Abstractions.Events;
using Callora.Host.Backend.Application.Events;
using Callora.Host.Backend.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Callora.Host.Backend.Infrastructure.Events;

/// <summary>
/// Drops a plugin's dedicated database schema when it is uninstalled
/// (PLAT-260): the plugin's own EF tables live in <c>plugin_&lt;id&gt;</c>,
/// so a single <c>DROP SCHEMA ... CASCADE</c> removes all of its data
/// cleanly. Idempotent — a plugin without a schema is a no-op.
/// </summary>
public sealed class PluginSchemaCleanupSubscriber(
    HostPersistenceDbContext dbContext,
    ILogger<PluginSchemaCleanupSubscriber> logger) : IHostApplicationEventSubscriber<PluginLifecycleChangedEvent>
{
    public async Task HandleAsync(PluginLifecycleChangedEvent appEvent, CancellationToken cancellationToken = default)
    {
        if (!appEvent.IsSuccess ||
            !string.Equals(appEvent.Action?.Trim(), "plugin.uninstall", StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(appEvent.PluginId))
        {
            return;
        }

        var schema = PluginSchemaName.TryResolve(appEvent.PluginId);
        if (schema is null)
        {
            return;
        }

        try
        {
            // Schema name is validated to a safe identifier (PluginSchemaName)
            // and built via concatenation — a DDL identifier cannot be a bound
            // parameter, so this is intentional raw SQL.
            var dropSql = "DROP SCHEMA IF EXISTS \"" + schema + "\" CASCADE;";
            await dbContext.Database
                .ExecuteSqlRawAsync(dropSql, cancellationToken)
                .ConfigureAwait(false);
            logger.LogInformation("Dropped schema {Schema} for uninstalled plugin {PluginId}.", schema, appEvent.PluginId);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Dropping schema {Schema} for plugin {PluginId} failed.", schema, appEvent.PluginId);
        }
    }
}
