using Callora.Host.Backend.Application.Abstractions.Events;
using Callora.Modules.Abstractions.Application.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using VoipHost.PluginContracts.Application.Events;

namespace Callora.Host.Backend.Infrastructure.Events;

/// <summary>
/// Default dispatcher that resolves and invokes all subscribers for one host event type.
/// </summary>
public sealed class HostApplicationEventDispatcher(
    IServiceProvider services,
    ICalloraPluginCatalog pluginCatalog,
    ILogger<HostApplicationEventDispatcher> logger) : IHostApplicationEventDispatcher
{
    public async Task DispatchAsync<TEvent>(TEvent appEvent, CancellationToken cancellationToken = default)
        where TEvent : IHostEvent
    {
        ArgumentNullException.ThrowIfNull(appEvent);

        var hostHandlers = services
            .GetServices<IHostApplicationEventSubscriber<TEvent>>()
            .ToArray();
        var pluginHandlers = pluginCatalog
            .GetExports(typeof(IHostEventSubscriber<TEvent>))
            .OfType<IHostEventSubscriber<TEvent>>()
            .ToArray();

        var handlers = hostHandlers
            .Select(x => new DispatchHandler<TEvent>(GetPriority(x), x.HandleAsync))
            .Concat(pluginHandlers.Select(x => new DispatchHandler<TEvent>(x.Priority, x.HandleAsync)))
            .OrderByDescending(x => x.Priority)
            .ToArray();

        foreach (var handler in handlers)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (appEvent is IHostEventPropagationState propagationState &&
                propagationState.IsPropagationStopped)
            {
                break;
            }

            try
            {
                await handler.Callback(appEvent, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning(
                    ex,
                    "Host event subscriber callback failed for event {EventType}. Continuing with remaining subscribers.",
                    typeof(TEvent).Name);
            }
        }
    }

    private static int GetPriority<TEvent>(IHostApplicationEventSubscriber<TEvent> handler)
        where TEvent : IHostEvent
        => handler is IHostApplicationEventSubscriberPriority prioritized
            ? prioritized.Priority
            : 0;
}
