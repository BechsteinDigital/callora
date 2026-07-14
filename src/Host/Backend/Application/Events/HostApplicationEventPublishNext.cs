using Callora.Host.PluginContracts.Application.Events;

namespace Callora.Host.Backend.Application.Events;

/// <summary>
/// Represents the next publish step in the host application event decorator pipeline.
/// </summary>
public delegate Task HostApplicationEventPublishNext<TEvent>(
    TEvent appEvent,
    CancellationToken cancellationToken)
    where TEvent : IHostEvent;
