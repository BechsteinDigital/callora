using Callora.Core.Application.Events;
using Callora.Core.Application.Extensions;
using Callora.Core.Application.Lifecycle;

namespace Callora.Core.Infrastructure.Events;

public sealed class ThemeJsonWorkspaceTemplateSyncSubscriber(
    IThemeJsonWorkspaceTemplateSyncService syncService,
    ILogger<ThemeJsonWorkspaceTemplateSyncSubscriber> logger) : IHostApplicationEventSubscriber<PluginLifecycleChangedEvent>
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
                    await HandleInstallOrUpdateAsync(pluginId, appEvent.Metadata, cancellationToken).ConfigureAwait(false);
                    break;
                case PluginLifecycleActions.Uninstall:
                    await syncService.ClearPluginDefinitionsAsync(pluginId, cancellationToken).ConfigureAwait(false);
                    break;
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Theme definition sync failed for plugin {PluginId} on action {Action}.", pluginId, action);
        }
    }

    private async Task HandleInstallOrUpdateAsync(
        string pluginId,
        IReadOnlyDictionary<string, string>? metadata,
        CancellationToken cancellationToken)
    {
        if (metadata is null ||
            !metadata.TryGetValue("assemblyPath", out var assemblyPath) ||
            string.IsNullOrWhiteSpace(assemblyPath))
        {
            return;
        }

        var version = ResolveVersion(metadata);
        if (string.IsNullOrWhiteSpace(version))
        {
            return;
        }

        await syncService
            .SyncFromAssemblyAsync(pluginId, version, assemblyPath, cancellationToken)
            .ConfigureAwait(false);
    }

    private static string ResolveVersion(IReadOnlyDictionary<string, string> metadata)
    {
        if (metadata.TryGetValue("registryVersion", out var registryVersion) && !string.IsNullOrWhiteSpace(registryVersion))
        {
            return registryVersion.Trim();
        }

        if (metadata.TryGetValue("packageVersion", out var packageVersion) && !string.IsNullOrWhiteSpace(packageVersion))
        {
            return packageVersion.Trim();
        }

        return string.Empty;
    }
}
