using System;
using Callora.Plugin.Communication.Application.RealtimeMedia;

namespace Callora.Core.Tests.Communication.Conference;

/// <summary>
/// A hand-written <see cref="IRemoteMediaTrack"/> double for the conference SFU tests: a remote track the
/// test materialises on a <see cref="FakeMediaPeer"/> and drives frames through via
/// <see cref="RaiseFrame"/>, so the router's inbound-subscribe → fan-out path can be exercised without a
/// real media stack.
/// </summary>
internal sealed class FakeRemoteMediaTrack : IRemoteMediaTrack
{
    public FakeRemoteMediaTrack(MediaTrackKind kind, string? streamId)
    {
        Kind = kind;
        StreamId = streamId;
    }

    public MediaTrackKind Kind { get; }

    public string? StreamId { get; }

    public event EventHandler<MediaFrame>? FrameReceived;

    /// <summary>Raises one encoded frame on this track, as the media stack would on receive.</summary>
    public void RaiseFrame(MediaFrame frame) => FrameReceived?.Invoke(this, frame);
}
