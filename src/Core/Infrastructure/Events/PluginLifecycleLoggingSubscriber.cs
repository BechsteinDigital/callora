using Callora.Core.Application.Events;

namespace Callora.Core.Infrastructure.Events;

public sealed class PluginLifecycleLoggingSubscriber(
    ILogger<PluginLifecycleLoggingSubscriber> logger) : IHostApplicationEventSubscriber<PluginLifecycleChangedEvent>
{
    public Task HandleAsync(PluginLifecycleChangedEvent appEvent, CancellationToken cancellationToken = default)
    {
        if (appEvent.IsSuccess)
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation(
                    "Plugin lifecycle event {Action} for {PluginId} succeeded.",
                    appEvent.Action,
                    appEvent.PluginId);
            }
        }
        else
        {
            if (logger.IsEnabled(LogLevel.Warning))
            {
                logger.LogWarning(
                    "Plugin lifecycle event {Action} for {PluginId} failed: {Message}",
                    appEvent.Action,
                    appEvent.PluginId,
                    appEvent.Message);
            }
        }

        return Task.CompletedTask;
    }
}
