namespace Callora.Plugin.Communication.Application.Calls;

/// <summary>
/// Publishes call transitions to whoever is watching this process right now.
/// </summary>
/// <remarks>
/// This is the live path, and it is deliberately separate from the outbox. The outbox is durable and
/// delivers to external consumers on a job cadence — right for a webhook, far too slow for a dialer
/// that has to light up while the phone is still ringing. Publishing here is best effort by design:
/// an event nobody is subscribed to is dropped, and a subscriber that cannot keep up loses its oldest
/// events rather than stalling the call.
/// </remarks>
public interface ICallEventPublisher
{
    /// <summary>Publishes one transition. Never blocks and never throws.</summary>
    void Publish(CallEventNotification notification);
}
