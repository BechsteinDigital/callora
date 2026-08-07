using Callora.Core.Application.Surfaces.Contracts;
using Callora.Plugin.Communication.Abstractions;
using Callora.Plugin.Communication.Application.Calls;

namespace Callora.Plugin.Communication.Api.Surface;

/// <summary>
/// Turns call transitions into surface context, so a block on a workplace lights up when the phone
/// does.
/// </summary>
/// <remarks>
/// <para>It sits beside the live event stream rather than replacing it: a dialer and the event
/// WebSocket keep their subscription, and the context is a second face of the same transition. That
/// is why it decorates <see cref="ICallEventPublisher"/> — one call path, two audiences, no second
/// place that has to be kept in step.</para>
/// <para>Published to the workspace and no further. A narrower address would be a guess as long as a
/// call belongs to nobody in particular; the day a call is assigned to a subject, the same
/// publication becomes a narrower one and no block changes (design §5.3).</para>
/// </remarks>
public sealed class SurfaceCallContextPublisher(
    ISurfaceContextBroadcaster surface,
    ICallEventPublisher? inner = null) : ICallEventPublisher
{
    private const string Inbound = "Inbound";
    private const string Ringing = "Ringing";
    private const string Connected = "Connected";
    private const string Terminated = "Terminated";

    // Which call each key currently describes, so another call's ending does not clear this one.
    private readonly Lock _gate = new();
    private string? _incomingCallId;
    private string? _activeCallId;

    /// <inheritdoc />
    public void Publish(CallEventNotification notification)
    {
        ArgumentNullException.ThrowIfNull(notification);

        inner?.Publish(notification);

        try
        {
            Project(notification);
        }
        catch (Exception)
        {
            // Published on the path that is handling a live call. A panel that cannot be told about a
            // call is a stale panel; an exception here would be a lost call because of one.
        }
    }

    private void Project(CallEventNotification notification)
    {
        var address = new SurfaceContextAddress(notification.WorkspaceKey);
        var identity = notification.InboundIdentity;
        var view = new SurfaceCallView(
            notification.CallId,
            notification.RemoteParty,
            notification.Direction,
            notification.State,
            notification.OccurredAt,
            identity?.CallerDisplayName,
            identity?.CalledNumber,
            identity?.DivertedFrom,
            // Nicht die beglaubigte Nummer selbst: Zwei Ziffernfolgen nebeneinander helfen niemandem
            // am Telefon. Was zählt, ist ob jemand für den Anrufer geradesteht.
            Verified: !string.IsNullOrWhiteSpace(identity?.AssertedIdentity));

        lock (_gate)
        {
            if (notification.State == Terminated)
            {
                Clear(address, notification.CallId);
                return;
            }

            // An outbound call rings too — at the other end. Reporting it as incoming would light the
            // panel up on every dial attempt.
            if (notification.State == Ringing && notification.Direction == Inbound)
            {
                _incomingCallId = notification.CallId;
                surface.Publish(address, SurfaceCallContextKeys.IncomingCall, view);
                return;
            }

            if (notification.State != Connected)
            {
                return;
            }

            // Both at once: a panel that leaves the incoming call standing while the conversation is
            // already running shows two truths at the same time.
            if (_incomingCallId == notification.CallId)
            {
                _incomingCallId = null;
                surface.Publish(address, SurfaceCallContextKeys.IncomingCall, null);
            }

            _activeCallId = notification.CallId;
            surface.Publish(address, SurfaceCallContextKeys.ActiveCall, view);
        }
    }

    private void Clear(SurfaceContextAddress address, string callId)
    {
        if (_incomingCallId == callId)
        {
            _incomingCallId = null;
            surface.Publish(address, SurfaceCallContextKeys.IncomingCall, null);
        }

        if (_activeCallId == callId)
        {
            _activeCallId = null;
            surface.Publish(address, SurfaceCallContextKeys.ActiveCall, null);
        }
    }
}
