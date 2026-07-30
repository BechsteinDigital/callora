namespace Callora.Plugin.Communication.Application.RealtimeMedia;

/// <summary>
/// The lifecycle state of an <see cref="IMediaPeer"/> — the neutral projection of a WebRTC peer's
/// connection state (RFC 8829). A provider adapter maps its SDK's peer-connection state onto these values,
/// so the media layer above the port never sees an SDK-specific enum.
/// </summary>
internal enum MediaConnectionState
{
    /// <summary>The peer was created but negotiation has not started.</summary>
    New,

    /// <summary>ICE/DTLS negotiation is in progress.</summary>
    Connecting,

    /// <summary>The transport is established and media can flow.</summary>
    Connected,

    /// <summary>Connectivity was lost but may recover.</summary>
    Disconnected,

    /// <summary>Negotiation failed unrecoverably.</summary>
    Failed,

    /// <summary>The peer was closed and will not transition further.</summary>
    Closed,
}
