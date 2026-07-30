namespace Callora.Plugin.Communication.Application.RealtimeMedia;

/// <summary>
/// A remote media track surfaced by an <see cref="IMediaPeer"/> — the neutral projection of the SDK's
/// per-track receive model (W3C ontrack). Encoded frames arrive on <see cref="FrameReceived"/>; a
/// forwarding layer subscribes synchronously when the track is received and copies each frame's payload,
/// which is valid only for the callback.
/// </summary>
internal interface IRemoteMediaTrack
{
    /// <summary>The media kind of this track.</summary>
    MediaTrackKind Kind { get; }

    /// <summary>The remote MediaStream id (a=msid) — the source participant for SFU forwarding — or
    /// <see langword="null"/> when the remote advertised none.</summary>
    string? StreamId { get; }

    /// <summary>Raised with each encoded frame received on this track.</summary>
    event EventHandler<MediaFrame>? FrameReceived;
}
