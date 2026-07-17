using Callora.Core.Application.Events;
using Callora.Core.Application.Lifecycle;
using Callora.Core.Application.Persistence;
using Callora.Core.Infrastructure.Persistence;

namespace Callora.Core.Infrastructure.Events;

/// <summary>
/// Drops a plugin's dedicated database schema when it is uninstalled
/// (PLAT-260): the plugin's own EF tables live in a dedicated schema, so a
/// single DROP SCHEMA removes all of its data cleanly. The schema name is
/// taken from the plugin manifest's "databaseSchema" field when present,
/// otherwise from the plugin_&lt;id&gt; convention. Idempotent — a plugin
/// without a schema is a no-op.
/// </summary>
public sealed class PluginSchemaCleanupSubscriber(
    IPluginSchemaDropper schemaDropper,
    IPluginDataDocumentCleaner dataDocumentCleaner,
    IPluginInstallationRepository installationRepository,
    ILogger<PluginSchemaCleanupSubscriber> logger) : IHostApplicationEventSubscriber<PluginLifecycleChangedEvent>
{
    public async Task HandleAsync(PluginLifecycleChangedEvent appEvent, CancellationToken cancellationToken = default)
    {
        if (!appEvent.IsSuccess ||
            !string.Equals(appEvent.Action?.Trim(), PluginLifecycleActions.Uninstall, StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(appEvent.PluginId))
        {
            return;
        }

        var pluginId = appEvent.PluginId.Trim();

        // Host-owned data documents live in the host schema keyed by plugin id,
        // so they are not covered by the dedicated-schema drop and must be
        // removed even for plugins without a dedicated schema.
        try
        {
            var removed = await dataDocumentCleaner.DeleteByPluginIdAsync(pluginId, cancellationToken).ConfigureAwait(false);
            if (removed > 0)
            {
                logger.LogInformation("Removed {Count} data documents for uninstalled plugin {PluginId}.", removed, pluginId);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Removing data documents for plugin {PluginId} failed.", pluginId);
        }

        var schema = await ResolveSchemaAsync(pluginId, cancellationToken).ConfigureAwait(false);
        if (schema is null)
        {
            return;
        }

        try
        {
            await schemaDropper.DropAsync(schema, cancellationToken).ConfigureAwait(false);
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
