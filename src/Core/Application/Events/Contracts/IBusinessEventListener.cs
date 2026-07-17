using Callora.Core.Extensibility;

namespace Callora.Core.Application.Events.Contracts;

/// <summary>
/// Receives every business event published on the bus. Host rails (flow
/// trigger, webhook relay) and plugins implement this to react generically;
/// listeners filter by <see cref="IBusinessEvent.EventName"/> themselves.
/// Plugins export listeners via <c>IHostPluginContext.Export</c>.
/// </summary>
[CalloraExtensible("Extension point — implement and export to react to business events (REV2 §8.2)")]
public interface IBusinessEventListener
{
    /// <summary>Execution order — higher runs earlier.</summary>
    int Priority { get; }

    /// <summary>Handles one published business event.</summary>
    Task OnBusinessEventAsync(IBusinessEvent businessEvent, CancellationToken cancellationToken = default);
}
