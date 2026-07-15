using Callora.Core.Application.Events;
using Callora.Core.Application.Plugins;

namespace Callora.Core.Infrastructure.Events;

public sealed class PluginUiAssetPublishSubscriber(
    IPluginUiAssetPublisher assetPublisher,
    ILogger<PluginUiAssetPublishSubscriber> logger) : IHostApplicationEventSubscriber<PluginLifecycleChangedEvent>
{
    public async Task HandleAsync(PluginLifecycleChangedEvent appEvent, CancellationToken cancellationToken = default)
    {
        if (!appEvent.IsSuccess)
        {
            return;
        }

        var action = appEvent.Action?.Trim();
        if (!IsPublishTrigger(action))
        {
            return;
        }

        try
        {
            await assetPublisher.PublishAllAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Plugin UI asset publish failed after lifecycle action {Action}.", action);
        }
    }

    private static bool IsPublishTrigger(string? action) =>
        string.Equals(action, "plugin.install", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(action, "plugin.update", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(action, "plugin.uninstall", StringComparison.OrdinalIgnoreCase) ||
        // Activation and deactivation change which plugins are Active, so the
        // published asset set must be rebuilt too — otherwise a deactivated
        // plugin keeps serving stale UI assets (§9.3 teardown).
        string.Equals(action, "plugin.activate", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(action, "plugin.deactivate", StringComparison.OrdinalIgnoreCase);
}
