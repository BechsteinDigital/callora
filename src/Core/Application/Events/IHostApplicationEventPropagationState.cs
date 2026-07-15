using Callora.Host.PluginContracts.Application.Events;

namespace Callora.Core.Application.Events;

/// <summary>
/// Exposes propagation state for events that can stop further subscriber execution.
/// </summary>
public interface IHostApplicationEventPropagationState : IHostEventPropagationState
{
}
