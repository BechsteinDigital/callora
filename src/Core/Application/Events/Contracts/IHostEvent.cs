using Callora.Core.Extensibility;

namespace Callora.Core.Application.Events.Contracts;

/// <summary>
/// Marker contract for host-dispatched events shared between host and runtime plugins.
/// </summary>
[CalloraExtensible("Extension point — implement to define a plugin host event (REV2 §8.2)")]
public interface IHostEvent
{
}
