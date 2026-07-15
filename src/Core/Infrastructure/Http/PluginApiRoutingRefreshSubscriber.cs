using Callora.Core.Application.Events;

namespace Callora.Core.Infrastructure.Http;

/// <summary>
/// Rebuilds the plugin routing table whenever a plugin lifecycle change is
/// published — activation adds routes, deactivation removes them (PLAT-257).
/// </summary>
public sealed class PluginApiRoutingRefreshSubscriber(PluginApiEndpointDataSource endpointDataSource)
    : IHostApplicationEventSubscriber<PluginLifecycleChangedEvent>
{
    public Task HandleAsync(PluginLifecycleChangedEvent appEvent, CancellationToken cancellationToken = default)
    {
        endpointDataSource.Refresh();
        return Task.CompletedTask;
    }
}
