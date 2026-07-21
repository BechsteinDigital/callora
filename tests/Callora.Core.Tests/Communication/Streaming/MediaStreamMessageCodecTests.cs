using Callora.Plugin.Communication.Application.Streaming.Protocol;
using Xunit;

namespace Callora.Core.Tests.Communication.Streaming;

/// <summary>
/// Wire-format round-trips of the Twilio-Media-Streams-style codec (B4a-2): each event encodes to
/// the expected JSON shape and decodes back; malformed/unknown frames decode to <c>null</c> so a
/// misbehaving consumer cannot crash the bridge.
/// </summary>
public sealed class MediaStreamMessageCodecTests
{
    [Fact]
    public void Media_RoundTrips_WithPayload()
    {
        var json = MediaStreamMessageCodec.Encode(MediaStreamMessage.Media("AQID"));

        Assert.Contains("\"event\":\"media\"", json);
        Assert.Contains("\"payload\":\"AQID\"", json);

        var decoded = MediaStreamMessageCodec.TryDecode(json);
        Assert.NotNull(decoded);
        Assert.Equal(MediaStreamEventType.Media, decoded!.Event);
        Assert.Equal("AQID", decoded.Payload);
    }

    [Fact]
    public void Mark_RoundTrips_WithName()
    {
        var json = MediaStreamMessageCodec.Encode(MediaStreamMessage.ForMark("greeting-done"));

        var decoded = MediaStreamMessageCodec.TryDecode(json);
        Assert.NotNull(decoded);
        Assert.Equal(MediaStreamEventType.Mark, decoded!.Event);
        Assert.Equal("greeting-done", decoded.MarkName);
    }

    [Fact]
    public void Clear_And_Stop_Decode()
    {
        Assert.Equal(MediaStreamEventType.Clear, MediaStreamMessageCodec.TryDecode("{\"event\":\"clear\"}")!.Event);
        Assert.Equal(MediaStreamEventType.Stop, MediaStreamMessageCodec.TryDecode("{\"event\":\"stop\"}")!.Event);
    }

    [Fact]
    public void Start_RoundTrips_MediaFormat()
    {
        var json = MediaStreamMessageCodec.Encode(
            MediaStreamMessage.ForStart(new MediaStreamStartMetadata("sess-1", "call-1", "audio/x-mulaw", 8000)));

        Assert.Contains("\"event\":\"start\"", json);
        Assert.Contains("\"encoding\":\"audio/x-mulaw\"", json);
        Assert.Contains("\"sampleRate\":8000", json);

        var decoded = MediaStreamMessageCodec.TryDecode(json);
        Assert.NotNull(decoded);
        Assert.Equal(MediaStreamEventType.Start, decoded!.Event);
        Assert.Equal("sess-1", decoded.Start!.SessionId);
        Assert.Equal("call-1", decoded.Start.CallId);
        Assert.Equal("audio/x-mulaw", decoded.Start.Encoding);
        Assert.Equal(8000, decoded.Start.SampleRateHz);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{}")]
    [InlineData("{\"event\":\"unknown\"}")]
    [InlineData("[]")]
    [InlineData("")]
    public void Malformed_Or_Unknown_DecodesToNull(string json)
    {
        Assert.Null(MediaStreamMessageCodec.TryDecode(json));
    }
}
