using VoipHost.PluginContracts.Application.Events;

namespace Callora.Host.Backend.Application.Abstractions.Events;

/// <summary>
/// Decorates host event publishing, for example with logging, policy checks, or tracing.
/// </summary>
public interface IHostApplicationEventPublisherDecorator
{
    /// <summary>
    /// Gets the decorator priority. Higher values run earlier in the publish pipeline.
    /// </summary>
    int DecorationPriority { get; }

    /// <summary>
    /// Handles one publish step and optionally forwards execution to the next pipeline step.
    /// </summary>
    Task PublishAsync<TEvent>(
        TEvent appEvent,
        HostApplicationEventPublishNext<TEvent> next,
        CancellationToken cancellationToken = default)
        where TEvent : IHostEvent;
}
