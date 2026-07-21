namespace Callora.Plugin.Communication.Abstractions;

/// <summary>
/// Payload of <see cref="ICommunicationChannel.IncomingCall"/>. The call is
/// in <see cref="CallState.Ringing"/> until accepted or rejected.
/// </summary>
public sealed class IncomingCallEventArgs : EventArgs
{
    /// <summary>Creates the payload for one inbound call.</summary>
    public IncomingCallEventArgs(ICall call)
    {
        ArgumentNullException.ThrowIfNull(call);
        Call = call;
    }

    /// <summary>The inbound call awaiting accept or reject.</summary>
    public ICall Call { get; }
}
