using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CalloraVoipSdk.WebRtc;

namespace Callora.Core.Tests.Communication.RealtimeMedia;

/// <summary>
/// A hand-written <see cref="IVideoTrack"/> double recording the options it was added with and the frames
/// the adapter's outbound track sends through it.
/// </summary>
internal sealed class FakeSdkVideoTrack(string mid, VideoTrackOptions options) : IVideoTrack
{
    public string Mid { get; } = mid;

    public TrackDirection Direction { get; } = options.Direction;

    public string? StreamId { get; } = options.StreamId;

    public List<(byte[] Payload, uint Timestamp)> SentFrames { get; } = [];

    public Task SendFrameAsync(
        ReadOnlyMemory<byte> encodedFrame,
        uint rtpTimestamp,
        CancellationToken cancellationToken = default)
    {
        SentFrames.Add((encodedFrame.ToArray(), rtpTimestamp));
        return Task.CompletedTask;
    }

    public Task SendFrameAsync(
        string rid,
        ReadOnlyMemory<byte> encodedFrame,
        uint rtpTimestamp,
        CancellationToken cancellationToken = default) =>
        SendFrameAsync(encodedFrame, rtpTimestamp, cancellationToken);
}
