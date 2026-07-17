using Callora.Core.Application.Events;
using Callora.Core.Application.Lifecycle;
using Callora.Core.Infrastructure.CustomFields;

namespace Callora.Core.Infrastructure.Events;

/// <summary>
/// Keeps custom field definitions in sync with the registry.json customFields
/// section across plugin install/update/uninstall.
/// </summary>
public sealed class PluginCustomFieldSyncSubscriber(
    RegistryCustomFieldSyncService syncService,
    ILogger<PluginCustomFieldSyncSubscriber> logger) : IHostApplicationEventSubscriber<PluginLifecycleChangedEvent>
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
                        var version = appEvent.Metadata.TryGetValue("registryVersion", out var v) && !string.IsNullOrWhiteSpace(v)
                            ? v.Trim()
                            : "0.0.0";
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
            logger.LogWarning(ex, "Custom field sync failed for plugin {PluginId} on action {Action}.", pluginId, action);
        }
    }
}
