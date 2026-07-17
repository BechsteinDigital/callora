using Callora.Core.Extensibility;

namespace Callora.Core.Application.Events.Contracts;

/// <summary>
/// Runtime plugin subscriber contract for host events.
/// </summary>
[CalloraExtensible("Extension point — implement and export to subscribe to a host event (REV2 §8.2)")]
public interface IHostEventSubscriber<in TEvent>
    where TEvent : IHostEvent
{
    /// <summary>
    /// Gets execution priority. Higher values run earlier.
    /// </summary>
    int Priority { get; }

    /// <summary>
    /// Handles one event instance.
    /// </summary>
    Task HandleAsync(TEvent appEvent, CancellationToken cancellationToken = default);
}
