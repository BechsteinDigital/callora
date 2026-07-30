using System;
using Callora.Plugin.Communication.Application.RealtimeMedia;
using Callora.Plugin.Communication.Infrastructure.RealtimeMedia;
using CalloraVoipSdk.WebRtc;
using Xunit;

namespace Callora.Core.Tests.Communication.RealtimeMedia;

/// <summary>
/// The frame/kind projection at the receive boundary: an incoming SDK <see cref="EncodedFrame"/> becomes a
/// neutral <see cref="MediaFrame"/> carrying the same payload, RTP timestamp and key-frame flag plus the
/// remote track's stream id; and the SDK <see cref="TrackKind"/> maps onto the neutral kind. The SDK
/// <see cref="RemoteTrack"/> event source is not constructible from tests, so the wrapper delegates this
/// pure mapping to the mapper under test.
/// </summary>
public sealed class CalloraVoipSdkFrameMapperTests
{
    [Fact]
    public void ToMediaFrame_ProjectsEveryField()
    {
        var payload = new byte[] { 4, 5, 6, 7 };
        var frame = new EncodedFrame(payload, 9001u, true, null, null);

        var mapped = CalloraVoipSdkFrameMapper.ToMediaFrame(frame, "participant-A");

        Assert.Equal(payload, mapped.Payload.ToArray());
        Assert.Equal(9001u, mapped.RtpTimestamp);
        Assert.True(mapped.IsKeyFrame);
        Assert.Equal("participant-A", mapped.StreamId);
    }

    [Fact]
    public void ToMediaFrame_CarriesNullTimestampAndStreamId()
    {
        var frame = new EncodedFrame(new byte[] { 1 }, null, false, null, null);

        var mapped = CalloraVoipSdkFrameMapper.ToMediaFrame(frame, streamId: null);

        Assert.Null(mapped.RtpTimestamp);
        Assert.False(mapped.IsKeyFrame);
        Assert.Null(mapped.StreamId);
    }

    [Fact]
    public void ToMediaTrackKind_MapsAudio() =>
        Assert.Equal(MediaTrackKind.Audio, CalloraVoipSdkFrameMapper.ToMediaTrackKind(TrackKind.Audio));

    [Fact]
    public void ToMediaTrackKind_MapsVideo() =>
        Assert.Equal(MediaTrackKind.Video, CalloraVoipSdkFrameMapper.ToMediaTrackKind(TrackKind.Video));

    [Fact]
    public void ToMediaTrackKind_ThrowsForUnknownKind() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => CalloraVoipSdkFrameMapper.ToMediaTrackKind((TrackKind)99));
}
