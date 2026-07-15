using Callora.Plugin.Communication.Abstractions;
using Callora.Plugin.Communication.Application.Audio;
using Callora.Core.Tests.Support;
using Xunit;

namespace Callora.Core.Tests.Application.Communication;

/// <summary>
/// Contract proof: call audio flows over the channel-neutral contracts —
/// these tests use the protocol-free fake channel, not SIP.
/// </summary>
public sealed class CallAudioContractTests
{
    [Fact]
    public async Task ConnectedCall_StreamsAudioBidirectionally_OverContractChannel()
    {
        var channel = new StaticCommunicationChannel("fake-voice");
        var call = channel.SimulateIncomingCall(new CallTarget("+4930111"));
        await call.AcceptAsync();

        await using var stream = await call.OpenAudioAsync();
        var receivedFrames = new List<AudioFrame>();
        stream.FrameReceived += (_, args) => receivedFrames.Add(args.Frame);

        await stream.SendAsync(new AudioFrame(new byte[160], TimeSpan.FromMilliseconds(20)));
        var backing = Assert.Single(call.OpenedAudioStreams);
        backing.RaiseFrameReceived(new AudioFrame(new byte[160], TimeSpan.FromMilliseconds(20)));

        Assert.Equal("PCMU", stream.Format.Codec);
        Assert.Equal(8000, stream.Format.ClockRate);
        Assert.Single(backing.SentFrames);
        var received = Assert.Single(receivedFrames);
        Assert.Equal(TimeSpan.FromMilliseconds(20), received.Duration);
    }

    [Fact]
    public async Task OpeningAudio_OnNotConnectedCall_Throws()
    {
        // Audio is voip-specific media, no longer on the neutral ICall contract,
        // so this exercises it on the concrete call in a not-connected state.
        var call = new StaticCall(new CallTarget("+4930111"), initialState: CallState.Connecting);

        await Assert.ThrowsAsync<InvalidOperationException>(() => call.OpenAudioAsync());
    }

    [Fact]
    public async Task MultipleAudioStreams_CanBeOpenInParallel()
    {
        var channel = new StaticCommunicationChannel("fake-voice");
        var call = channel.SimulateIncomingCall(new CallTarget("+4930111"));
        await call.AcceptAsync();

        await using var first = await call.OpenAudioAsync();
        await using var second = await call.OpenAudioAsync();

        Assert.Equal(2, call.OpenedAudioStreams.Count);
        Assert.NotSame(first, second);
    }
}
