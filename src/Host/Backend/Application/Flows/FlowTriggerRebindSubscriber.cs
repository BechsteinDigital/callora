using Callora.Host.Backend.Application.Abstractions.Events;
using Callora.Host.Backend.Application.Events;

namespace Callora.Host.Backend.Application.Flows;

/// <summary>
/// Rebinds the flow trigger to the exported call event streams whenever a
/// plugin lifecycle change occurs — activation adds streams, deactivation
/// removes them (PLAT-257).
/// </summary>
public sealed class FlowTriggerRebindSubscriber(FlowTrigger flowTrigger)
    : IHostApplicationEventSubscriber<PluginLifecycleChangedEvent>
{
    public Task HandleAsync(PluginLifecycleChangedEvent appEvent, CancellationToken cancellationToken = default)
    {
        flowTrigger.RefreshBindings();
        return Task.CompletedTask;
    }
}
