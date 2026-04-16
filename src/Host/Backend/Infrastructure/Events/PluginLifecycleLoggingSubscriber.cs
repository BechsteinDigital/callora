using Callora.Host.Backend.Application.Abstractions.Events;
using Callora.Host.Backend.Application.Events;

namespace Callora.Host.Backend.Infrastructure.Events;

public sealed class PluginLifecycleLoggingSubscriber(
    ILogger<PluginLifecycleLoggingSubscriber> logger) : IHostApplicationEventSubscriber<PluginLifecycleChangedEvent>
{
    public Task HandleAsync(PluginLifecycleChangedEvent appEvent, CancellationToken cancellationToken = default)
    {
        if (appEvent.IsSuccess)
        {
            logger.LogInformation(
                "Plugin lifecycle event {Action} for {PluginId} succeeded.",
                appEvent.Action,
                appEvent.PluginId);
        }
        else
        {
            logger.LogWarning(
                "Plugin lifecycle event {Action} for {PluginId} failed: {Message}",
                appEvent.Action,
                appEvent.PluginId,
                appEvent.Message);
        }

        return Task.CompletedTask;
    }
}
