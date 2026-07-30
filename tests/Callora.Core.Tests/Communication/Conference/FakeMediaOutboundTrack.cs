using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Callora.Plugin.Communication.Application.RealtimeMedia;

namespace Callora.Core.Tests.Communication.Conference;

/// <summary>
/// A hand-written <see cref="IMediaOutboundTrack"/> double for the conference SFU tests. Records the kind
/// and stream it was created for and every frame the router sends on it, so a test can assert the fan-out
/// topology (which source's track a consumer received) and that payload/timestamp were carried through.
/// </summary>
internal sealed class FakeMediaOutboundTrack : IMediaOutboundTrack
{
    private readonly List<MediaFrame> _sentFrames = [];

    public FakeMediaOutboundTrack(MediaTrackKind kind, string streamId)
    {
        Kind = kind;
        StreamId = streamId;
    }

    /// <summary>The kind this track was added for.</summary>
    public MediaTrackKind Kind { get; }

    /// <summary>The source participant id this track renders (its MediaStream id).</summary>
    public string StreamId { get; }

    /// <summary>Every frame the router forwarded onto this track, in order.</summary>
    public IReadOnlyList<MediaFrame> SentFrames => _sentFrames;

    public Task SendFrameAsync(MediaFrame frame, CancellationToken ct = default)
    {
        _sentFrames.Add(frame);
        return Task.CompletedTask;
    }
}
