using Callora.Host.Backend.Application.Abstractions.Events;
using Callora.Host.Backend.Application.Abstractions.Persistence;
using Callora.Host.Backend.Application.Events;
using Callora.Host.Backend.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Callora.Host.Backend.Infrastructure.Events;

/// <summary>
/// Drops a plugin's dedicated database schema when it is uninstalled
/// (PLAT-260): the plugin's own EF tables live in a dedicated schema, so a
/// single <c>DROP SCHEMA ... CASCADE</c> removes all of its data cleanly.
/// The schema name is taken from the plugin manifest's "databaseSchema"
/// field when present, otherwise from the plugin_&lt;id&gt; convention.
/// Idempotent — a plugin without a schema is a no-op.
/// </summary>
public sealed class PluginSchemaCleanupSubscriber(
    HostPersistenceDbContext dbContext,
    IPluginInstallationRepository installationRepository,
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

        var pluginId = appEvent.PluginId.Trim();
        var schema = await ResolveSchemaAsync(pluginId, cancellationToken).ConfigureAwait(false);
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
            logger.LogInformation("Dropped schema {Schema} for uninstalled plugin {PluginId}.", schema, pluginId);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Dropping schema {Schema} for plugin {PluginId} failed.", schema, pluginId);
        }
    }

    private async Task<string?> ResolveSchemaAsync(string pluginId, CancellationToken cancellationToken)
    {
        // Prefer the manifest declaration; the installation record survives
        // uninstall (state change, not deletion), so its assembly path still
        // points at the registry.json.
        var installation = await installationRepository
            .GetByPluginIdAsync(pluginId, cancellationToken)
            .ConfigureAwait(false);
        if (installation is not null &&
            PluginManifestSchemaReader.TryReadDatabaseSchema(installation.AssemblyPath) is { } declared)
        {
            return declared;
        }

        return PluginSchemaName.TryResolve(pluginId);
    }
}
