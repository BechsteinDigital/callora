using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Callora.Plugin.Communication.Abstractions;
using Callora.Plugin.Communication.Application.Streaming;
using Callora.Plugin.Communication.Application.Streaming.Pacing;
using Callora.Core.Tests.Communication.Conference;
using Xunit;

namespace Callora.Core.Tests.Communication.Streaming;

/// <summary>
/// Playing prepared audio into a live call — the mechanics under every announcement: a dial-in asking
/// for a PIN, an IVR menu, a voice agent. Turning text into audio is somebody else's job; this only
/// pushes bytes at the right cadence.
/// </summary>
public sealed class CallAudioPlaybackServiceTests
{
    private const string Workspace = "ws-a";
    private const string CallId = "call-1";

    [Fact]
    public async Task Play_SendsTheAudioAsFramesOfTheCallsFormat()
    {
        var (service, stream, clock) = NewService();

        await using var playback = await service.PlayAsync(
            Workspace, CallId, Announcement(frames: 3), AudioFormat.G711Ulaw8k20ms);
        await TickUntil(clock, () => stream.Sent.Count == 3);

        Assert.Equal(3, stream.Sent.Count);
        Assert.All(stream.Sent, frame => Assert.Equal(160, frame.Length));
    }

    [Fact]
    public async Task Play_CompletesWhenTheAudioHasBeenPlayedToTheEnd()
    {
        var (service, stream, clock) = NewService();

        await using var playback = await service.PlayAsync(
            Workspace, CallId, Announcement(frames: 2), AudioFormat.G711Ulaw8k20ms);
        await TickUntil(clock, () => stream.Sent.Count == 2);

        await playback.Completion.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Play_PadsATrailingPartialFrameInsteadOfClipping()
    {
        var (service, stream, clock) = NewService();
        var audio = new byte[160 + 40];
        Array.Fill(audio, (byte)0x7F);

        await using var playback = await service.PlayAsync(
            Workspace, CallId, audio, AudioFormat.G711Ulaw8k20ms);
        await TickUntil(clock, () => stream.Sent.Count == 2);

        // Sending a short frame would clip the last word; dropping it would swallow it. Padding to a
        // full frame keeps the audio intact and costs at most 20 ms of silence.
        Assert.Equal(2, stream.Sent.Count);
        Assert.Equal(160, stream.Sent[1].Length);
    }

    [Fact]
    public async Task Dispose_StopsThePlaybackAtOnce()
    {
        var (service, stream, clock) = NewService();
        var playback = await service.PlayAsync(
            Workspace, CallId, Announcement(frames: 50), AudioFormat.G711Ulaw8k20ms);
        await TickUntil(clock, () => stream.Sent.Count == 1);

        await playback.DisposeAsync();
        var afterDispose = stream.Sent.Count;
        clock.Tick();
        clock.Tick();
        await Task.Delay(50);

        // Barge-in is the expected course, not an exception: whoever knows the PIN types over the
        // greeting, and the greeting has to stop mid-word.
        Assert.Equal(afterDispose, stream.Sent.Count);
    }

    [Fact]
    public async Task Dispose_LetsCompletionFinishRatherThanFault()
    {
        var (service, _, _) = NewService();
        var playback = await service.PlayAsync(
            Workspace, CallId, Announcement(frames: 50), AudioFormat.G711Ulaw8k20ms);

        await playback.DisposeAsync();

        // A caller who interrupts is not an error, so awaiting Completion after a barge-in must not
        // throw at whoever was waiting for the announcement to end.
        await playback.Completion.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task ASecondPlay_ReplacesTheFirstInsteadOfQueueingBehindIt()
    {
        var (service, stream, clock) = NewService();
        var first = await service.PlayAsync(
            Workspace, CallId, Announcement(frames: 50), AudioFormat.G711Ulaw8k20ms);
        await TickUntil(clock, () => stream.Sent.Count == 1);

        await using var second = await service.PlayAsync(
            Workspace, CallId, Announcement(frames: 2), AudioFormat.G711Ulaw8k20ms);

        // Queueing would make the device sound like it is repeating itself: nobody wants to hear
        // "please enter your PIN" after they already have.
        await first.Completion.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Play_WithAFormatTheCallCannotCarry_FailsByName()
    {
        var (service, _, _) = NewService();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.PlayAsync(
                Workspace, CallId, Announcement(frames: 1), new AudioFormat(AudioCodec.G711Alaw, 8_000, 20)));

        // Playing A-law bytes down a µ-law call is not silence — it is loud noise in somebody's ear.
        Assert.Contains("format", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Play_ToACallOfAnotherWorkspace_Fails()
    {
        var (service, _, _) = NewService();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.PlayAsync("ws-other", CallId, Announcement(frames: 1), AudioFormat.G711Ulaw8k20ms));
    }

    [Fact]
    public async Task Play_ToACallWithoutLiveAudio_Fails()
    {
        var calls = new FakeCallAccess(Workspace, new FakePlaybackCall(CallId));
        var provider = new SdkCallAudioStreamProviderStub();
        var service = new CallAudioPlaybackService(calls, provider, _ => new ManualPacingClock());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.PlayAsync(Workspace, CallId, Announcement(frames: 1), AudioFormat.G711Ulaw8k20ms));
    }

    private static (CallAudioPlaybackService Service, FakeAttachedAudioStream Stream, ManualPacingClock Clock) NewService()
    {
        var call = new FakePlaybackCall(CallId);
        var stream = new FakeAttachedAudioStream();
        var provider = new SdkCallAudioStreamProviderStub();
        provider.Register(CallId, stream);
        var clock = new ManualPacingClock();
        return (new CallAudioPlaybackService(new FakeCallAccess(Workspace, call), provider, _ => clock), stream, clock);
    }

    private static byte[] Announcement(int frames)
    {
        var audio = new byte[frames * 160];
        Array.Fill(audio, (byte)0x7F);
        return audio;
    }

    private static async Task TickUntil(ManualPacingClock clock, Func<bool> condition)
    {
        for (var i = 0; i < 200 && !condition(); i++)
        {
            clock.Tick();
            await Task.Delay(5);
        }
    }
}

/// <summary>A connected call that carries audio; the playback resolves it before touching the stream.</summary>
internal sealed class FakePlaybackCall(string callId) : IVoipCall
{
    public string CallId => callId;

    public CallState State => CallState.Connected;

    public CallDirection Direction => CallDirection.Inbound;

    public CallTarget Target => new("sip:caller@example.com");

    public CallTerminationReason? TerminationReason => null;

#pragma warning disable CS0067 // The playback observes neither.
    public event EventHandler<CallStateChangedEventArgs>? StateChanged;
    public event EventHandler<DtmfReceivedEventArgs>? DtmfReceived;
#pragma warning restore CS0067

    public Task<ICallAudioStream> OpenAudioAsync(CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException(
            "The playback must use the call's registered stream, not open a second tap.");

    public Task AcceptAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task RejectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task HangupAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task SendDtmfAsync(char tone, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

/// <summary>Stands in for the SDK-backed provider: the map of live streams by call id.</summary>
internal sealed class SdkCallAudioStreamProviderStub : ICallAudioStreamProvider
{
    private readonly System.Collections.Generic.Dictionary<string, ICallAudioStream> _streams = new(StringComparer.Ordinal);

    public void Register(string callId, ICallAudioStream stream) => _streams[callId] = stream;

    public Task<ICallAudioStream?> OpenAsync(string callId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_streams.GetValueOrDefault(callId));
}
