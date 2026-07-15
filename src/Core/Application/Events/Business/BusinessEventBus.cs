using Callora.Host.PluginContracts.Application.Events;
using Callora.Core.Application.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Callora.Core.Application.Events.Business;

/// <summary>
/// Fans one business event out to every listener — host rails resolved from
/// DI plus plugin-exported listeners — ordered by priority, isolating
/// failures so one bad listener does not stop the others (PLAT-270).
/// </summary>
public sealed class BusinessEventBus(
    IServiceProvider services,
    ICalloraPluginCatalog pluginCatalog,
    ILogger<BusinessEventBus> logger) : IBusinessEventBus
{
    public async Task PublishAsync(IBusinessEvent businessEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(businessEvent);

        var listeners = services
            .GetServices<IBusinessEventListener>()
            .Concat(pluginCatalog.GetExports<IBusinessEventListener>())
            .OrderByDescending(static listener => listener.Priority)
            .ToArray();

        foreach (var listener in listeners)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await listener.OnBusinessEventAsync(businessEvent, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning(
                    exception,
                    "Business event listener {Listener} failed for event {EventName}.",
                    listener.GetType().Name,
                    businessEvent.EventName);
            }
        }
    }
}
