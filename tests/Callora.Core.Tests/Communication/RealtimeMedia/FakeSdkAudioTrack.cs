using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CalloraVoipSdk.WebRtc;

namespace Callora.Core.Tests.Communication.RealtimeMedia;

/// <summary>
/// A hand-written <see cref="IAudioTrack"/> double recording the options it was added with and the frames
/// the adapter's outbound track sends through it.
/// </summary>
internal sealed class FakeSdkAudioTrack(string mid, AudioTrackOptions options) : IAudioTrack
{
    public string Mid { get; } = mid;

    public TrackDirection Direction { get; } = options.Direction;

    public string? StreamId { get; } = options.StreamId;

    public List<(byte[] Payload, uint Timestamp)> SentFrames { get; } = [];

    public Task SendFrameAsync(
        ReadOnlyMemory<byte> encodedAudioFrame,
        uint rtpTimestamp,
        CancellationToken cancellationToken = default)
    {
        SentFrames.Add((encodedAudioFrame.ToArray(), rtpTimestamp));
        return Task.CompletedTask;
    }
}
