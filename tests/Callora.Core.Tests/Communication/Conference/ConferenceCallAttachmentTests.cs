using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Callora.Plugin.Communication.Abstractions;
using Callora.Plugin.Communication.Abstractions.Conference;
using Callora.Plugin.Communication.Application.Conference;
using Callora.Plugin.Communication.Application.RealtimeMedia;
using Xunit;

namespace Callora.Core.Tests.Communication.Conference;

/// <summary>
/// Hanging a live call into a conference. This is the seam a policy plugin needs and cannot build for
/// itself: answering the SFU's offer requires a media engine, and mixing for a phone requires a
/// decoder — both live here, so the plugin asks rather than binds.
/// </summary>
public sealed class ConferenceCallAttachmentTests
{
    private const string Workspace = "ws-a";
    private const string Conf = "conf-1";

    [Fact]
    public async Task Attach_MakesTheCallerAudibleToTheOthers()
    {
        var fixture = await NewFixtureAsync();

        await using var leg = await fixture.Attachment.AttachAsync(Workspace, "call-1", Conf, "caller");
        fixture.Call.RaiseInboundAudio(MuLawTone());

        // The browser participant receives the caller on a track keyed by the caller's participant id,
        // exactly as it receives any other member.
        Assert.NotEmpty(FramesForCaller(fixture.BrowserPeer));
    }

    [Fact]
    public async Task Attach_ToAConferenceRequiringEndToEndEncryption_FailsByName()
    {
        var fixture = await NewFixtureAsync(policy: new ConferencePolicy(RequiresEndToEndEncryption: true));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => fixture.Attachment.AttachAsync(Workspace, "call-1", Conf, "caller"));

        // Naming the reason matters: this is a room that chose encryption over dial-in, not a fault.
        Assert.Contains("end-to-end", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Attach_ToAConferenceThisNodeDoesNotHost_ReportsItAsRouting()
    {
        var fixture = await NewFixtureAsync();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => fixture.Attachment.AttachAsync(Workspace, "call-1", "conference-elsewhere", "caller"));

        // Without this the empty conference would simply be created here and the caller would sit in a
        // room of one, wondering why nobody speaks. A routing problem must read as one.
        Assert.Contains("node", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Attach_ToACallThisWorkspaceDoesNotOwn_Fails()
    {
        var fixture = await NewFixtureAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => fixture.Attachment.AttachAsync("ws-other", "call-1", Conf, "caller"));
    }

    [Fact]
    public async Task Dispose_TakesTheCallOutOfTheConferenceWithoutEndingIt()
    {
        var fixture = await NewFixtureAsync();
        var leg = await fixture.Attachment.AttachAsync(Workspace, "call-1", Conf, "caller");

        await leg.DisposeAsync();

        // The caller is moved out of the room and stays on the line — a caller sent back to the lobby
        // has not hung up.
        Assert.False(fixture.Call.HungUp);
        var before = FramesForCaller(fixture.BrowserPeer).Count;
        fixture.Call.RaiseInboundAudio(MuLawTone());
        Assert.Equal(before, FramesForCaller(fixture.BrowserPeer).Count);
    }

    [Fact]
    public async Task SetMuted_KeepsTheCallerOutOfTheRoomServerSide()
    {
        var fixture = await NewFixtureAsync();
        await using var leg = await fixture.Attachment.AttachAsync(Workspace, "call-1", Conf, "caller");

        await leg.SetMutedAsync(true);
        fixture.Call.RaiseInboundAudio(MuLawTone());

        Assert.True(leg.IsMuted);
        Assert.Empty(FramesForCaller(fixture.BrowserPeer));
    }

    private static async Task<AttachmentFixture> NewFixtureAsync(ConferencePolicy? policy = null)
    {
        var provider = new FakeRealtimeMediaProvider();
        var service = new ConferenceService(provider, new MediaPeerOptions { EnableVideo = true });

        // A conference only exists once somebody is in it, so the room is opened by a browser member.
        var browser = policy is null
            ? await service.JoinAsync(Conf, "browser")
            : await service.JoinAsync(Conf, "browser", policy);
        var browserPeer = Assert.IsType<FakeMediaPeer>(Assert.IsType<ConferenceParticipant>(browser).Peer);
        browserPeer.RaiseConnectionStateChanged(MediaConnectionState.Connected);

        var call = new FakeAttachableCall("call-1");
        var attachment = new ConferenceCallAttachment(
            service,
            new FakeCallAccess(Workspace, call),
            new Callora.Plugin.Communication.Infrastructure.RealtimeMedia.SdkAudioTranscoderFactory());

        return new AttachmentFixture(attachment, call, browserPeer);
    }

    private static List<MediaFrame> FramesForCaller(FakeMediaPeer peer)
    {
        var frames = new List<MediaFrame>();
        foreach (var track in peer.OutboundTracks)
        {
            if (track.StreamId == "caller")
            {
                frames.AddRange(track.SentFrames);
            }
        }

        return frames;
    }

    private static byte[] MuLawTone()
    {
        using var encoder = new Callora.Plugin.Communication.Infrastructure.RealtimeMedia.SdkAudioTranscoderFactory()
            .Create(ConferenceAudioCodec.G711Ulaw, 8_000);
        var pcm = new byte[320];
        for (var i = 0; i < 160; i++)
        {
            BitConverter.TryWriteBytes(pcm.AsSpan(i * 2), (short)(8000 * Math.Sin(2 * Math.PI * 400 * i / 8000.0)));
        }

        return encoder.EncodeFromPcm16(pcm);
    }

    private sealed record AttachmentFixture(
        ConferenceCallAttachment Attachment,
        FakeAttachableCall Call,
        FakeMediaPeer BrowserPeer);
}

/// <summary>Resolves exactly one call, for one workspace — the boundary the real service enforces.</summary>
internal sealed class FakeCallAccess(string workspaceKey, IVoipCall call) : ICallAccess
{
    public ICall? Find(string ws, string callId) =>
        string.Equals(ws, workspaceKey, StringComparison.OrdinalIgnoreCase) && callId == call.CallId ? call : null;
}

/// <summary>A connected call whose audio stream the test drives directly.</summary>
internal sealed class FakeAttachableCall(string callId) : IVoipCall
{
    private readonly FakeAttachedAudioStream _audio = new();

    public string CallId => callId;

    public CallState State => CallState.Connected;

    public CallDirection Direction => CallDirection.Inbound;

    public CallTarget Target => new("sip:caller@example.com");

    public CallTerminationReason? TerminationReason => null;

    public bool HungUp { get; private set; }

    public List<byte[]> SentToCaller => _audio.Sent;

#pragma warning disable CS0067 // The attachment does not observe these.
    public event EventHandler<CallStateChangedEventArgs>? StateChanged;
    public event EventHandler<DtmfReceivedEventArgs>? DtmfReceived;
#pragma warning restore CS0067

    public void RaiseInboundAudio(byte[] frame) => _audio.RaiseFrame(frame);

    public Task<ICallAudioStream> OpenAudioAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<ICallAudioStream>(_audio);

    public Task AcceptAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task RejectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task HangupAsync(CancellationToken cancellationToken = default)
    {
        HungUp = true;
        return Task.CompletedTask;
    }

    public Task SendDtmfAsync(char tone, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

/// <summary>The call's audio stream: what the test pushes in, and what the mix sends back out.</summary>
internal sealed class FakeAttachedAudioStream : ICallAudioStream
{
    public AudioFormat Format => AudioFormat.G711Ulaw8k20ms;

    public List<byte[]> Sent { get; } = [];

    public bool Disposed { get; private set; }

    public event EventHandler<AudioFrameReceivedEventArgs>? FrameReceived;

    public void RaiseFrame(byte[] frame) => FrameReceived?.Invoke(this, new AudioFrameReceivedEventArgs(frame));

    public ValueTask SendAsync(ReadOnlyMemory<byte> frame, CancellationToken cancellationToken = default)
    {
        Sent.Add(frame.ToArray());
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        Disposed = true;
        return ValueTask.CompletedTask;
    }
}
