namespace Callora.Core.Application.Events.Contracts;

/// <summary>
/// Central publish surface for business events (PLAT-270). Host subsystems
/// and plugins publish named events here; the platform fans them out to
/// every listener — flows, webhooks, notifications and plugin subscribers —
/// without the publisher knowing the consumers.
/// </summary>
public interface IBusinessEventBus
{
    /// <summary>Publishes one business event to all listeners.</summary>
    Task PublishAsync(IBusinessEvent businessEvent, CancellationToken cancellationToken = default);
}
