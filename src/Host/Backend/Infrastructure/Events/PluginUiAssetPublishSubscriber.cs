using Callora.Host.Backend.Application.Abstractions.Events;
using Callora.Host.Backend.Application.Abstractions.Plugins;
using Callora.Host.Backend.Application.Events;

namespace Callora.Host.Backend.Infrastructure.Events;

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
        string.Equals(action, "plugin.uninstall", StringComparison.OrdinalIgnoreCase);
}
