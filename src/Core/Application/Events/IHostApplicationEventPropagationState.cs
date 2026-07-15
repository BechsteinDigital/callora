using Callora.Core.Application.Events.Contracts;

namespace Callora.Core.Application.Events;

/// <summary>
/// Exposes propagation state for events that can stop further subscriber execution.
/// </summary>
public interface IHostApplicationEventPropagationState : IHostEventPropagationState
{
}
