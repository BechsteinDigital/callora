namespace VoipHost.PluginContracts.Application.Events;

/// <summary>
/// Runtime plugin subscriber contract for host events.
/// </summary>
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
