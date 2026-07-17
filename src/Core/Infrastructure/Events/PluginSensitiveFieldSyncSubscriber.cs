using Callora.Core.Application.Events;
using Callora.Core.Application.Lifecycle;
using Callora.Core.Infrastructure.Webhooks;

namespace Callora.Core.Infrastructure.Events;

/// <summary>
/// Keeps the webhook sensitive-field registry in sync with the registry.json
/// "sensitiveFields" section across plugin install/update/uninstall (PLAT-244),
/// so a plugin's person-related payload fields are masked without the core
/// hardcoding them.
/// </summary>
public sealed class PluginSensitiveFieldSyncSubscriber(
    RegistrySensitiveFieldSyncService syncService,
    ILogger<PluginSensitiveFieldSyncSubscriber> logger) : IHostApplicationEventSubscriber<PluginLifecycleChangedEvent>
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
                        await syncService
                            .SyncFromAssemblyAsync(pluginId, assemblyPath, cancellationToken)
                            .ConfigureAwait(false);
                    }

                    break;
                case PluginLifecycleActions.Uninstall:
                    syncService.ClearPlugin(pluginId);
                    break;
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Sensitive-field sync failed for plugin {PluginId} on action {Action}.", pluginId, action);
        }
    }
}
