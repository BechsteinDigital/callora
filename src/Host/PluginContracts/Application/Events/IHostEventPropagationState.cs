namespace VoipHost.PluginContracts.Application.Events;

/// <summary>
/// Exposes propagation state for events that can stop further subscriber execution.
/// </summary>
public interface IHostEventPropagationState
{
    /// <summary>
    /// Gets a value indicating whether subscriber propagation is stopped.
    /// </summary>
    bool IsPropagationStopped { get; }
}
