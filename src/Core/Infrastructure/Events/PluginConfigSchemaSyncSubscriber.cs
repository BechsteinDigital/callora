using Callora.Core.Application.Events;
using Callora.Core.Application.Lifecycle;
using Callora.Core.Infrastructure.Configuration;

namespace Callora.Core.Infrastructure.Events;

/// <summary>
/// Keeps system config definitions in sync with the registry.json config
/// schema across plugin install/update/uninstall.
/// </summary>
public sealed class PluginConfigSchemaSyncSubscriber(
    RegistryConfigSchemaSyncService syncService,
    ILogger<PluginConfigSchemaSyncSubscriber> logger) : IHostApplicationEventSubscriber<PluginLifecycleChangedEvent>
{
    public async Task HandleAsync(PluginLifecycleChangedEvent appEvent, CancellationToken cancellationToken = default)
    {
        if (!appEvent.IsSuccess || string.IsNullOrWhiteSpace(appEvent.PluginId))
        {
            return;
        }

        var action = appEvent.Action?.Trim();
        var pluginId = appEvent.PluginId.Trim();

        try
        {
            switch (action)
            {
                case PluginLifecycleActions.Install:
                case PluginLifecycleActions.Update:
                    if (appEvent.Metadata is not null &&
                        appEvent.Metadata.TryGetValue("assemblyPath", out var assemblyPath) &&
                        !string.IsNullOrWhiteSpace(assemblyPath))
                    {
                        var version = ResolveVersion(appEvent.Metadata);
                        await syncService
                            .SyncFromAssemblyAsync(pluginId, version, assemblyPath, cancellationToken)
                            .ConfigureAwait(false);
                    }
                    break;
                case PluginLifecycleActions.Uninstall:
                    await syncService.ClearPluginDefinitionsAsync(pluginId, cancellationToken).ConfigureAwait(false);
                    break;
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Config schema sync failed for plugin {PluginId} on action {Action}.", pluginId, action);
        }
    }

    private static string ResolveVersion(IReadOnlyDictionary<string, string> metadata)
    {
        if (metadata.TryGetValue("registryVersion", out var registryVersion) && !string.IsNullOrWhiteSpace(registryVersion))
        {
            return registryVersion.Trim();
        }

        return metadata.TryGetValue("packageVersion", out var packageVersion) && !string.IsNullOrWhiteSpace(packageVersion)
            ? packageVersion.Trim()
            : "0.0.0";
    }
}
