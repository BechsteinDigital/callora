using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Callora.Plugin.Communication.Abstractions;
using Callora.Plugin.Communication.Infrastructure.Sdk;
using CalloraVoipSdk.Core.Application.Media;
using CalloraVoipSdk.Core.Domain.Lines;
using Xunit;
using NativeCall = CalloraVoipSdk.Core.Domain.Calls.ICall;
using SdkCallState = CalloraVoipSdk.Core.Domain.Calls.CallState;
using SdkIncomingCallEventArgs = CalloraVoipSdk.Core.Domain.Events.IncomingCallEventArgs;

namespace Callora.Core.Tests.Communication.Sdk;

/// <summary>
/// The SDK phone-line → foundation voice-channel adapter (B4-deep-2b): registration state becomes
/// channel health, inbound SDK calls surface as foundation <see cref="IVoipCall"/>s, outbound dials
/// are wrapped the same way, and dispose unsubscribes from the line.
/// </summary>
public sealed class SdkVoiceChannelTests
{
    [Theory]
    [InlineData(LineState.Registered, ChannelHealth.Up)]
    [InlineData(LineState.Reconnecting, ChannelHealth.Degraded)]
    [InlineData(LineState.RegistrationFailed, ChannelHealth.Degraded)]
    [InlineData(LineState.Failed, ChannelHealth.Down)]
    [InlineData(LineState.Unregistered, ChannelHealth.Unknown)]
    [InlineData(LineState.Registering, ChannelHealth.Unknown)]
    public void Health_MapsEveryLineState(LineState lineState, ChannelHealth expected)
    {
        var channel = NewChannel(new FakePhoneLine { State = lineState });

        Assert.Equal(expected, channel.Health);
    }

    [Fact]
    public void Identity_ProjectsCtorValues_AndDeclaresVoice()
    {
        var channel = new SdkVoiceChannel("ch-1", "Berlin Trunk", "callora.communication", new FakePhoneLine(), Tap, maxConcurrentCalls: 10);

        Assert.Equal("ch-1", channel.ChannelId);
        Assert.Equal("Berlin Trunk", channel.DisplayName);
        Assert.Equal("callora.communication", channel.PluginId);
        Assert.Contains(CommunicationCapabilities.Voice, channel.Capabilities);
    }

    [Fact]
    public async Task PlaceCallAsync_DialsLine_AndWrapsAsVoipCall()
    {
        var line = new FakePhoneLine { DialResult = new FakeSdkCall { RemoteParty = "sip:bob@example.com" } };
        var channel = NewChannel(line);

        var call = await channel.PlaceCallAsync(new CallTarget("sip:bob@example.com"));

        Assert.Equal("sip:bob@example.com", line.DialedTarget);
        Assert.IsAssignableFrom<IVoipCall>(call);
        Assert.Equal("sip:bob@example.com", call.Target.Value);
    }

    [Fact]
    public async Task PlaceCallAsync_NullTarget_Throws()
    {
        var channel = NewChannel(new FakePhoneLine { DialResult = new FakeSdkCall() });

        await Assert.ThrowsAsync<ArgumentNullException>(() => channel.PlaceCallAsync(null!));
    }

    [Fact]
    public void IncomingCall_WrapsSdkCall_AndRaisesFoundationEvent()
    {
        var line = new FakePhoneLine();
        var channel = NewChannel(line);
        var sdkCall = new FakeSdkCall { RemoteParty = "sip:carol@example.com" };
        IncomingCallEventArgs? raised = null;
        channel.IncomingCall += (_, e) => raised = e;

        line.RaiseIncomingCall(sdkCall);

        Assert.NotNull(raised);
        Assert.IsAssignableFrom<IVoipCall>(raised!.Call);
        Assert.Equal("sip:carol@example.com", raised.Call.Target.Value);
    }

    [Fact]
    public void IncomingCall_NoSubscriber_DoesNotThrow()
    {
        var line = new FakePhoneLine();
        _ = NewChannel(line);

        line.RaiseIncomingCall(new FakeSdkCall()); // must not throw despite no channel subscriber
    }

    [Fact]
    public void Dispose_UnsubscribesFromLine()
    {
        var line = new FakePhoneLine();
        var channel = NewChannel(line);
        var count = 0;
        channel.IncomingCall += (_, _) => count++;

        channel.Dispose();
        line.RaiseIncomingCall(new FakeSdkCall());

        Assert.Equal(0, count);
        Assert.False(line.HasIncomingCallSubscribers);
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        var line = new FakePhoneLine();
        var channel = NewChannel(line);

        channel.Dispose();
        channel.Dispose(); // second dispose is a no-op, must not throw
    }

    [Fact]
    public async Task ProducedCall_CanOpenTheAudioBridge()
    {
        var receiver = new FakeMediaReceiver();
        var sender = new FakeMediaSender();
        var line = new FakePhoneLine { DialResult = new FakeSdkCall() };
        var channel = new SdkVoiceChannel("ch", "Line", "plugin", line, () => (receiver, sender), maxConcurrentCalls: 10);

        var call = Assert.IsAssignableFrom<IVoipCall>(await channel.PlaceCallAsync(new CallTarget("sip:x@example.com")));
        await using var audio = await call.OpenAudioAsync();

        Assert.Same(line.DialResult, receiver.AttachedCall); // tap attached to the dialed SDK call
        byte[]? inbound = null;
        audio.FrameReceived += (_, e) => inbound = e.Frame.ToArray();
        receiver.RaiseFrame(new MediaFrame(new byte[] { 9 }, PayloadType: 0, DurationRtpUnits: 160));
        Assert.Equal(new byte[] { 9 }, inbound);
    }

    [Fact]
    public void HealthChanged_RaisedOnLineHealthTransition()
    {
        var line = new FakePhoneLine { State = LineState.Registered };
        using var channel = NewChannel(line);
        ChannelHealth? raised = null;
        channel.HealthChanged += (_, e) => raised = e.Health;

        line.RaiseStateChanged(LineState.Registered, LineState.Failed);

        Assert.Equal(ChannelHealth.Down, raised); // Registered(Up) → Failed(Down)
    }

    [Fact]
    public void HealthChanged_SuppressedWhenMappedHealthUnchanged()
    {
        var line = new FakePhoneLine { State = LineState.Reconnecting };
        using var channel = NewChannel(line);
        var count = 0;
        channel.HealthChanged += (_, _) => count++;

        // Reconnecting and RegistrationFailed both map to Degraded → no visible health transition.
        line.RaiseStateChanged(LineState.Reconnecting, LineState.RegistrationFailed);

        Assert.Equal(0, count);
    }

    [Fact]
    public void Dispose_UnsubscribesFromLineStateChanges()
    {
        var line = new FakePhoneLine { State = LineState.Registered };
        var channel = NewChannel(line);
        var count = 0;
        channel.HealthChanged += (_, _) => count++;

        channel.Dispose();
        line.RaiseStateChanged(LineState.Registered, LineState.Failed);

        Assert.Equal(0, count); // no health event after dispose
    }

    // ── S3: MaxConcurrentCalls enforcement ─────────────────────────────────────

    [Fact]
    public async Task PlaceCall_AtLimit_Throws()
    {
        // max=1, one active outbound call → second PlaceCallAsync must throw.
        var sdkCall1 = new FakeSdkCall { State = SdkCallState.Connected };
        var sdkCall2 = new FakeSdkCall { State = SdkCallState.Idle };
        var line = new FakePhoneLine { DialResult = sdkCall1 };
        var channel = new SdkVoiceChannel("ch", "Line", "plugin", line, Tap, maxConcurrentCalls: 1);

        _ = await channel.PlaceCallAsync(new CallTarget("sip:a@example.com")); // occupies the slot

        line.DialResult = sdkCall2;
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            channel.PlaceCallAsync(new CallTarget("sip:b@example.com")));
    }

    [Fact]
    public async Task PlaceCall_AfterTerminated_Succeeds_Again()
    {
        // After the first call terminates (counter decrements), a new outbound call must succeed.
        var sdkCall1 = new FakeSdkCall { State = SdkCallState.Connected };
        var line = new FakePhoneLine { DialResult = sdkCall1 };
        var channel = new SdkVoiceChannel("ch", "Line", "plugin", line, Tap, maxConcurrentCalls: 1);

        _ = await channel.PlaceCallAsync(new CallTarget("sip:a@example.com"));

        // Terminate the first call → counter decrements.
        sdkCall1.RaiseStateChanged(SdkCallState.Connected, SdkCallState.Terminated);

        var sdkCall2 = new FakeSdkCall { State = SdkCallState.Idle };
        line.DialResult = sdkCall2;
        var call2 = await channel.PlaceCallAsync(new CallTarget("sip:b@example.com")); // must not throw

        Assert.NotNull(call2);
    }

    [Fact]
    public async Task InboundCountsAgainstLimit_BlocksOutbound()
    {
        // An inbound call occupies a slot, so outbound at max=1 must throw.
        var sdkInbound = new FakeSdkCall { State = SdkCallState.Ringing };
        var sdkOutbound = new FakeSdkCall { State = SdkCallState.Idle };
        var line = new FakePhoneLine { DialResult = sdkOutbound };
        var channel = new SdkVoiceChannel("ch", "Line", "plugin", line, Tap, maxConcurrentCalls: 1);
        channel.IncomingCall += (_, _) => { }; // subscribe so inbound is counted

        line.RaiseIncomingCall(sdkInbound); // fills the slot

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            channel.PlaceCallAsync(new CallTarget("sip:b@example.com")));
    }

    [Fact]
    public void InboundAtLimit_IsRefusedAsBusy()
    {
        // Counting an inbound call the account has no line for made the ceiling one-sided: outbound
        // was blocked while inbound kept arriving, so the trunk ran over its own limit.
        var occupying = new FakeSdkCall { State = SdkCallState.Ringing };
        var arriving = new FakeSdkCall { State = SdkCallState.Ringing };
        var line = new FakePhoneLine();
        var channel = new SdkVoiceChannel("ch", "Line", "plugin", line, Tap, maxConcurrentCalls: 1);
        var delivered = 0;
        channel.IncomingCall += (_, _) => delivered++;

        line.RaiseIncomingCall(occupying);
        line.RaiseIncomingCall(arriving);

        // 486, not the drain's 503: a full line is occupied and will free up, and 503 invites a
        // carrier to mark the whole trunk unreachable over a momentary peak.
        Assert.Equal(1, delivered);
        Assert.True(arriving.RejectCalled);
        Assert.Equal(486, arriving.RejectStatusCode);
    }

    [Fact]
    public void ARefusedInboundCall_DoesNotOccupyALine()
    {
        // Otherwise the refusal itself would fill the trunk: every rejected call would keep a line
        // counted, and the channel would never recover.
        var occupying = new FakeSdkCall { State = SdkCallState.Ringing };
        var refused = new FakeSdkCall { State = SdkCallState.Ringing };
        var next = new FakeSdkCall { State = SdkCallState.Ringing };
        var line = new FakePhoneLine();
        var channel = new SdkVoiceChannel("ch", "Line", "plugin", line, Tap, maxConcurrentCalls: 1);
        var delivered = 0;
        channel.IncomingCall += (_, _) => delivered++;

        line.RaiseIncomingCall(occupying);
        line.RaiseIncomingCall(refused);
        occupying.RaiseStateChanged(SdkCallState.Ringing, SdkCallState.Terminated);
        line.RaiseIncomingCall(next);

        Assert.Equal(2, delivered);
        Assert.False(next.RejectCalled);
    }

    [Fact]
    public async Task AnInboundCallAtTheLimit_DoesNotBlockTheNextOutboundCall()
    {
        // The refused call must give its reservation back, or one busy moment would cost a line for
        // the life of the channel.
        var occupying = new FakeSdkCall { State = SdkCallState.Ringing };
        var refused = new FakeSdkCall { State = SdkCallState.Ringing };
        var line = new FakePhoneLine();
        var channel = new SdkVoiceChannel("ch", "Line", "plugin", line, Tap, maxConcurrentCalls: 1);
        channel.IncomingCall += (_, _) => { };

        line.RaiseIncomingCall(occupying);
        line.RaiseIncomingCall(refused);
        occupying.RaiseStateChanged(SdkCallState.Ringing, SdkCallState.Terminated);

        line.DialResult = new FakeSdkCall { State = SdkCallState.Idle };
        Assert.NotNull(await channel.PlaceCallAsync(new CallTarget("sip:b@example.com")));
    }

    [Fact]
    public void InboundWithoutAConsumer_DoesNotOccupyALine()
    {
        // Nobody is going to answer it, so counting it would let an unwired channel starve itself.
        var arriving = new FakeSdkCall { State = SdkCallState.Ringing };
        var line = new FakePhoneLine();
        var channel = new SdkVoiceChannel("ch", "Line", "plugin", line, Tap, maxConcurrentCalls: 1);

        line.RaiseIncomingCall(arriving);

        var delivered = 0;
        channel.IncomingCall += (_, _) => delivered++;
        line.RaiseIncomingCall(new FakeSdkCall { State = SdkCallState.Ringing });

        Assert.Equal(1, delivered);
    }

    [Fact]
    public async Task DialFailure_ReleasesReservation_AllowingNextCall()
    {
        // If DialAsync throws, the reserved slot must be released so the next call can proceed.
        var line = new FakePhoneLine(); // DialResult is null → will throw
        var channel = new SdkVoiceChannel("ch", "Line", "plugin", line, Tap, maxConcurrentCalls: 1);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            channel.PlaceCallAsync(new CallTarget("sip:a@example.com")));

        // Provide a working result for the second attempt.
        line.DialResult = new FakeSdkCall { State = SdkCallState.Idle };
        var call = await channel.PlaceCallAsync(new CallTarget("sip:b@example.com")); // must not throw

        Assert.NotNull(call);
    }

    private static (IMediaReceiver Receiver, IMediaSender Sender) Tap() => (new FakeMediaReceiver(), new FakeMediaSender());

    private static SdkVoiceChannel NewChannel(FakePhoneLine line) =>
        new("ch", "Line", "plugin", line, Tap, maxConcurrentCalls: 10);
}

/// <summary>
/// A hand-written SDK <see cref="IPhoneLine"/> double. Only the members the channel touches are
/// functional (state, incoming-call event, dial); the rest throw. The SDK's inbound event args have
/// an internal ctor, so <see cref="RaiseIncomingCall"/> builds it reflectively.
/// </summary>
internal sealed class FakePhoneLine : IPhoneLine
{
    private static readonly ConstructorInfo IncomingArgsCtor =
        typeof(SdkIncomingCallEventArgs).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            types: [typeof(NativeCall)],
            modifiers: null)
        ?? throw new InvalidOperationException("SDK IncomingCallEventArgs ctor signature changed.");

    private static readonly ConstructorInfo StateArgsCtor =
        typeof(CalloraVoipSdk.Core.Domain.Events.LineStateChangedEventArgs).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            types: [typeof(LineState), typeof(LineState), typeof(IPhoneLine)],
            modifiers: null)
        ?? throw new InvalidOperationException("SDK LineStateChangedEventArgs ctor signature changed.");

    public LineState State { get; set; } = LineState.Registered;

    public NativeCall? DialResult { get; set; }

    public string? DialedTarget { get; private set; }

    public bool HasIncomingCallSubscribers => IncomingCall is not null;

    public event EventHandler<SdkIncomingCallEventArgs>? IncomingCall;

    public event EventHandler<CalloraVoipSdk.Core.Domain.Events.LineStateChangedEventArgs>? StateChanged;

#pragma warning disable CS0067 // Interface members the channel never observes.
    public event EventHandler<CalloraVoipSdk.Core.Domain.Events.LineReconnectingEventArgs>? LineReconnecting;
    public event EventHandler<CalloraVoipSdk.Core.Domain.Events.LineReconnectFailedEventArgs>? LineReconnectFailed;
    public event EventHandler<CalloraVoipSdk.Core.Domain.Events.OutboundCallRingingEventArgs>? OutboundCallRinging;
    public event EventHandler<CalloraVoipSdk.Core.Domain.Events.IncomingMessageEventArgs>? IncomingMessage;
#pragma warning restore CS0067

    public void RaiseIncomingCall(NativeCall call)
    {
        var args = (SdkIncomingCallEventArgs)IncomingArgsCtor.Invoke([call]);
        IncomingCall?.Invoke(this, args);
    }

    public void RaiseStateChanged(LineState oldState, LineState newState)
    {
        State = newState;
        var args = (CalloraVoipSdk.Core.Domain.Events.LineStateChangedEventArgs)StateArgsCtor.Invoke([oldState, newState, this]);
        StateChanged?.Invoke(this, args);
    }

    public Task<NativeCall> DialAsync(
        string targetUri,
        CalloraVoipSdk.Core.Domain.Calls.DialOptions? options = null,
        CancellationToken ct = default)
    {
        DialedTarget = targetUri;
        return Task.FromResult(DialResult ?? throw new InvalidOperationException("DialResult not set."));
    }

    /// <summary>How often the channel withdrew this line's registration (the drain path).</summary>
    public int UnregisterCalls { get; private set; }

    /// <summary>Set to make unregistering fail, so a drain can be shown to survive it.</summary>
    public Exception? UnregisterFailure { get; set; }

    public Task UnregisterAsync(CancellationToken ct = default)
    {
        UnregisterCalls++;
        return UnregisterFailure is null ? Task.CompletedTask : Task.FromException(UnregisterFailure);
    }

    // ── Members the channel must never touch ────────────────────────────────────
    public LineId LineId => throw new NotSupportedException();

    public SipAccount Account => throw new NotSupportedException();

    public Task SendMessageAsync(string targetUri, string body, string contentType = "text/plain", CancellationToken ct = default) =>
        throw new NotSupportedException();

    public Task<CalloraVoipSdk.Core.Domain.Publications.PublishResult> PublishAsync(
        string eventType, string body, string contentType = "text/plain", int expiresSeconds = 3600, CancellationToken ct = default) =>
        throw new NotSupportedException();

    public Task<CalloraVoipSdk.Core.Domain.Publications.PublishResult> RefreshPublicationAsync(
        string eventType, string etag, int expiresSeconds = 3600, CancellationToken ct = default) =>
        throw new NotSupportedException();

    public Task<CalloraVoipSdk.Core.Domain.Publications.PublishResult> ModifyPublicationAsync(
        string eventType, string etag, string body, string contentType = "text/plain", int expiresSeconds = 3600, CancellationToken ct = default) =>
        throw new NotSupportedException();

    public Task RemovePublicationAsync(string eventType, string etag, CancellationToken ct = default) =>
        throw new NotSupportedException();
}
