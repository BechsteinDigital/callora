using VoipHost.PluginContracts.Application.Events;

namespace Callora.Host.Backend.Application.Abstractions.Events;

/// <summary>
/// Exposes propagation state for events that can stop further subscriber execution.
/// </summary>
public interface IHostApplicationEventPropagationState : IHostEventPropagationState
{
}
