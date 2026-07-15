namespace Callora.Plugin.Communication.Abstractions;

/// <summary>
/// One configured communication channel inside a workspace, for example a
/// registered SIP trunk or a WebRTC gateway. Implementations are provided
/// and registered by communication plugins.
/// </summary>
public interface ICommunicationChannel
{
    /// <summary>Stable channel identifier unique within the workspace.</summary>
    string ChannelId { get; }

    /// <summary>Human-readable channel name shown to operators.</summary>
    string DisplayName { get; }

    /// <summary>Identifier of the plugin providing this channel.</summary>
    string PluginId { get; }

    /// <summary>
    /// Capability codes this channel provides, for example
    /// <see cref="CommunicationCapabilities.Voice"/>.
    /// </summary>
    IReadOnlyCollection<string> Capabilities { get; }

    /// <summary>
    /// Raised for each inbound call arriving on this channel. The call starts
    /// in <see cref="CallState.Ringing"/>; consumers answer it via
    /// <see cref="ICall.AcceptAsync"/> or <see cref="ICall.RejectAsync"/>.
    /// </summary>
    event EventHandler<IncomingCallEventArgs>? IncomingCall;

    /// <summary>
    /// Places one outbound call to the target. The returned call starts in
    /// <see cref="CallState.Connecting"/>.
    /// </summary>
    Task<ICall> PlaceCallAsync(CallTarget target, CancellationToken cancellationToken = default);
}
