using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Callora.Plugin.Communication.Abstractions;
using Callora.Plugin.Communication.Infrastructure.Sdk;
using CalloraVoipSdk.Core.Application.Media;
using Xunit;
using CallActionResult = CalloraVoipSdk.Core.Domain.Calls.CallActionResult;
using CallActionStatus = CalloraVoipSdk.Core.Domain.Calls.CallActionStatus;
using NativeCall = CalloraVoipSdk.Core.Domain.Calls.ICall;
using SdkCallDirection = CalloraVoipSdk.Core.Domain.Calls.CallDirection;
using SdkCallId = CalloraVoipSdk.Core.Domain.Calls.CallId;
using SdkCallState = CalloraVoipSdk.Core.Domain.Calls.CallState;
using SdkCallStateChangedEventArgs = CalloraVoipSdk.Core.Domain.Events.CallStateChangedEventArgs;
using SdkCallTerminatedBy = CalloraVoipSdk.Core.Domain.Calls.CallTerminatedBy;
using SdkCallTerminationCategory = CalloraVoipSdk.Core.Domain.Calls.CallTerminationCategory;
using SdkCallTerminationReason = CalloraVoipSdk.Core.Domain.Calls.CallTerminationReason;
using SdkDtmfReceivedEventArgs = CalloraVoipSdk.Core.Domain.Events.DtmfReceivedEventArgs;
using SdkDtmfTone = CalloraVoipSdk.Core.Domain.Calls.DtmfTone;

namespace Callora.Core.Tests.Communication.Sdk;

/// <summary>
/// The SDK↔foundation call adapter (B4-deep-2): maps the SDK's richer lifecycle onto the four
/// foundation states (collapsing hold/transfer onto Connected), forwards the four foundation actions
/// — translating the SDK's result-based reject back to the throwing foundation contract — and opens
/// the B4-deep-1 audio bridge on demand.
/// </summary>
public sealed class SdkCallTests
{
    [Fact]
    public void Properties_ProjectTheSdkCall()
    {
        var id = SdkCallId.New();
        var sdk = new FakeSdkCall
        {
            CallId = id,
            State = SdkCallState.Connected,
            Direction = SdkCallDirection.Inbound,
            RemoteParty = "sip:alice@example.com",
        };
        var call = NewCall(sdk);

        Assert.Equal(id.Value.ToString(), call.CallId);
        Assert.Equal(CallState.Connected, call.State);
        Assert.Equal(CallDirection.Inbound, call.Direction);
        Assert.Equal("sip:alice@example.com", call.Target.Value);
    }

    [Theory]
    [InlineData(SdkCallState.Idle, CallState.Connecting)]
    [InlineData(SdkCallState.Dialing, CallState.Connecting)]
    [InlineData(SdkCallState.Ringing, CallState.Ringing)]
    [InlineData(SdkCallState.Connected, CallState.Connected)]
    [InlineData(SdkCallState.OnHold, CallState.Connected)]
    [InlineData(SdkCallState.Transferring, CallState.Connected)]
    [InlineData(SdkCallState.Terminated, CallState.Terminated)]
    public void State_MapsEverySdkState(SdkCallState sdkState, CallState expected)
    {
        var call = NewCall(new FakeSdkCall { State = sdkState });

        Assert.Equal(expected, call.State);
    }

    [Theory]
    [InlineData(SdkCallDirection.Inbound, CallDirection.Inbound)]
    [InlineData(SdkCallDirection.Outbound, CallDirection.Outbound)]
    public void Direction_MapsBothDirections(SdkCallDirection sdkDirection, CallDirection expected)
    {
        var call = NewCall(new FakeSdkCall { Direction = sdkDirection });

        Assert.Equal(expected, call.Direction);
    }

    [Fact]
    public void TerminationReason_NullWhenSdkHasNone()
    {
        var call = NewCall(new FakeSdkCall { TerminationReason = null });

        Assert.Null(call.TerminationReason);
    }

    [Theory]
    [InlineData(SdkCallTerminationCategory.Completed, CallTerminationCategory.Completed)]
    [InlineData(SdkCallTerminationCategory.Busy, CallTerminationCategory.Busy)]
    [InlineData(SdkCallTerminationCategory.NoAnswer, CallTerminationCategory.NoAnswer)]
    [InlineData(SdkCallTerminationCategory.Rejected, CallTerminationCategory.Rejected)]
    [InlineData(SdkCallTerminationCategory.Canceled, CallTerminationCategory.Canceled)]
    [InlineData(SdkCallTerminationCategory.Failed, CallTerminationCategory.Failed)]
    public void TerminationReason_MapsEverySdkCategory(SdkCallTerminationCategory sdkCategory, CallTerminationCategory expected)
    {
        var sdk = new FakeSdkCall
        {
            TerminationReason = new SdkCallTerminationReason { Category = sdkCategory },
        };
        var call = NewCall(sdk);

        Assert.Equal(expected, call.TerminationReason!.Category);
    }

    [Fact]
    public void TerminationReason_CopiesProtocolDetail()
    {
        var sdk = new FakeSdkCall
        {
            TerminationReason = new SdkCallTerminationReason
            {
                Category = SdkCallTerminationCategory.Busy,
                SipStatusCode = 486,
                ReasonPhrase = "Busy Here",
                TerminatedBy = SdkCallTerminatedBy.Remote,
                RetryAfterSeconds = 30,
            },
        };
        var reason = NewCall(sdk).TerminationReason;

        Assert.NotNull(reason);
        Assert.Equal(CallTerminationCategory.Busy, reason!.Category);
        Assert.Equal(486, reason.SipStatusCode);
        Assert.Equal("Busy Here", reason.ReasonPhrase);
        Assert.Equal(CallTerminatedBy.Remote, reason.TerminatedBy);
        Assert.Equal(30, reason.RetryAfterSeconds);
    }

    [Fact]
    public void StateChanged_ReRaisesMappedTransition()
    {
        var sdk = new FakeSdkCall { State = SdkCallState.Dialing };
        var call = NewCall(sdk);
        CallStateChangedEventArgs? raised = null;
        call.StateChanged += (_, e) => raised = e;

        sdk.RaiseStateChanged(SdkCallState.Dialing, SdkCallState.Ringing);

        Assert.NotNull(raised);
        Assert.Equal(CallState.Connecting, raised!.PreviousState);
        Assert.Equal(CallState.Ringing, raised.CurrentState);
    }

    [Fact]
    public void StateChanged_SuppressesCollapsedNoOp()
    {
        var sdk = new FakeSdkCall { State = SdkCallState.Connected };
        var call = NewCall(sdk);
        var count = 0;
        call.StateChanged += (_, _) => count++;

        // Connected → OnHold both map to foundation Connected: no visible transition.
        sdk.RaiseStateChanged(SdkCallState.Connected, SdkCallState.OnHold);

        Assert.Equal(0, count);
    }

    [Fact]
    public void StateChanged_DetachesFromSdkAfterTerminated()
    {
        var sdk = new FakeSdkCall { State = SdkCallState.Connected };
        var call = NewCall(sdk);
        var count = 0;
        call.StateChanged += (_, _) => count++;

        sdk.RaiseStateChanged(SdkCallState.Connected, SdkCallState.Terminated);

        Assert.Equal(1, count);
        Assert.False(sdk.HasStateChangedSubscribers); // adapter unsubscribed — will not outlive the call
    }

    [Fact]
    public async Task AcceptAsync_ForwardsToSdk()
    {
        var sdk = new FakeSdkCall();
        var call = NewCall(sdk);

        await call.AcceptAsync();

        Assert.True(sdk.AcceptCalled);
    }

    [Fact]
    public async Task HangupAsync_ForwardsToSdk()
    {
        var sdk = new FakeSdkCall();
        var call = NewCall(sdk);

        await call.HangupAsync();

        Assert.True(sdk.HangupCalled);
    }

    [Fact]
    public async Task SendDtmfAsync_ForwardsMappedTone()
    {
        var sdk = new FakeSdkCall();
        var call = NewCall(sdk);

        await call.SendDtmfAsync('5');

        Assert.Equal('5', sdk.LastDtmf!.Value.Symbol);
    }

    [Fact]
    public async Task SendDtmfAsync_InvalidTone_Throws()
    {
        var call = NewCall(new FakeSdkCall());

        await Assert.ThrowsAsync<ArgumentException>(() => call.SendDtmfAsync('Z'));
    }

    [Fact]
    public void InboundIdentity_CarriesWhoCalledAndWhichNumberTheyReached()
    {
        var sdk = new FakeSdkCall
        {
            Direction = SdkCallDirection.Inbound,
            RemoteParty = "sip:+4930111@pbx.example.com",
            CalledNumber = "+4930222",
            RemoteNumber = "+4930111",
            RemoteDisplayName = "Alice",
        };

        var identity = NewCall(sdk).InboundIdentity;

        // The dialed number is what a consumer routes on — which of our numbers was called decides
        // whose call it is. The caller's name is what a screen-pop shows.
        Assert.NotNull(identity);
        Assert.Equal("+4930222", identity!.CalledNumber);
        Assert.Equal("+4930111", identity.CallerNumber);
        Assert.Equal("Alice", identity.CallerDisplayName);
    }

    [Fact]
    public void InboundIdentity_CarriesTheAssertedAndDivertedHeaders()
    {
        var sdk = new FakeSdkCall
        {
            Direction = SdkCallDirection.Inbound,
            AssertedIdentity = "sip:+4930999@trusted.example.com",
            Diversion = "sip:+4930888@pbx.example.com",
        };

        var identity = NewCall(sdk).InboundIdentity;

        // On a trunk the asserted identity is the caller you can believe, and the diversion says the
        // call reached you via somebody else's number — both change how a call should be handled.
        Assert.Equal("sip:+4930999@trusted.example.com", identity!.AssertedIdentity);
        Assert.Equal("sip:+4930888@pbx.example.com", identity.DivertedFrom);
    }

    [Fact]
    public void InboundIdentity_IsAbsentForAnOutboundCall()
    {
        var call = NewCall(new FakeSdkCall { Direction = SdkCallDirection.Outbound });

        // Nothing here applies when we placed the call; an empty record would invite consumers to
        // check five fields for null instead of one.
        Assert.Null(call.InboundIdentity);
    }

    [Fact]
    public void DtmfReceived_ReRaisesToneAndDurationFromSdk()
    {
        var sdk = new FakeSdkCall();
        var call = NewCall(sdk);
        DtmfReceivedEventArgs? raised = null;
        call.DtmfReceived += (_, e) => raised = e;

        sdk.RaiseDtmfReceived('7', durationMs: 120);

        Assert.NotNull(raised);
        Assert.Equal('7', raised!.Tone);
        Assert.Equal(120, raised.DurationMs);
    }

    [Fact]
    public void DtmfReceived_DetachesFromSdkAfterTerminated()
    {
        var sdk = new FakeSdkCall { State = SdkCallState.Connected };
        var call = NewCall(sdk);
        var count = 0;
        call.DtmfReceived += (_, _) => count++;

        sdk.RaiseStateChanged(SdkCallState.Connected, SdkCallState.Terminated);

        Assert.False(sdk.HasDtmfReceivedSubscribers); // adapter unsubscribed — will not outlive the call
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task RejectAsync_Success_CompletesWithoutThrowing()
    {
        var sdk = new FakeSdkCall { RejectResult = CallActionResult.Success() };
        var call = NewCall(sdk);

        await call.RejectAsync();

        Assert.True(sdk.RejectCalled);
    }

    [Fact]
    public async Task RejectAsync_InvalidState_ThrowsInvalidOperation()
    {
        var sdk = new FakeSdkCall
        {
            RejectResult = CallActionResult.Failure(CallActionStatus.InvalidState, "not ringing"),
        };
        var call = NewCall(sdk);

        await Assert.ThrowsAsync<InvalidOperationException>(() => call.RejectAsync());
    }

    [Fact]
    public async Task RejectAsync_Canceled_ThrowsOperationCanceled()
    {
        var sdk = new FakeSdkCall
        {
            RejectResult = CallActionResult.Failure(CallActionStatus.Canceled, "canceled"),
        };
        var call = NewCall(sdk);

        await Assert.ThrowsAsync<OperationCanceledException>(() => call.RejectAsync());
    }

    [Fact]
    public async Task OpenAudioAsync_AttachesTapAndProducesWorkingBridge()
    {
        var sdk = new FakeSdkCall();
        var receiver = new FakeMediaReceiver();
        var sender = new FakeMediaSender();
        var call = new SdkCall(sdk, () => (receiver, sender));

        await using var stream = await call.OpenAudioAsync();

        // Tap attached to this very call — the WS bridge (B4-deep-1) is now driven by the SDK call.
        Assert.Same(sdk, receiver.AttachedCall);
        Assert.Same(sdk, sender.AttachedCall);

        // Inbound: an SDK frame surfaces as a copied FrameReceived.
        byte[]? inbound = null;
        stream.FrameReceived += (_, e) => inbound = e.Frame.ToArray();
        receiver.RaiseFrame(new MediaFrame(new byte[] { 1, 2, 3 }, PayloadType: 0, DurationRtpUnits: 160));
        Assert.Equal(new byte[] { 1, 2, 3 }, inbound);

        // Outbound: SendAsync forwards a µ-law frame to the SDK sender.
        await stream.SendAsync(new byte[] { 4, 5 });
        Assert.Single(sender.Sent);
    }

    [Fact]
    public async Task OpenAudioAsync_AttachFailure_DisposesTap()
    {
        var sdk = new FakeSdkCall();
        var receiver = new FakeMediaReceiver();
        var sender = new ThrowingOnAttachSender();
        var call = new SdkCall(sdk, () => (receiver, sender));

        await Assert.ThrowsAsync<InvalidOperationException>(() => call.OpenAudioAsync());

        Assert.True(receiver.Disposed);
        Assert.True(sender.Disposed);
    }

    private static SdkCall NewCall(FakeSdkCall sdk) =>
        new(sdk, () => (new FakeMediaReceiver(), new FakeMediaSender()));
}

/// <summary>
/// A hand-written SDK <see cref="NativeCall"/> double. Only the members the adapter touches are
/// functional (identity, state, direction, remote party, the four forwarded actions and the state
/// event); everything else throws, documenting that the adapter must not depend on it.
/// </summary>
internal sealed class FakeSdkCall : NativeCall
{
    // The SDK's CallStateChangedEventArgs ctor is internal to the SDK assembly, so the test builds it
    // reflectively — the only way to exercise the adapter's real event contract from this assembly.
    private static readonly ConstructorInfo StateArgsCtor =
        typeof(SdkCallStateChangedEventArgs).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            types: [typeof(SdkCallState), typeof(SdkCallState), typeof(NativeCall), typeof(SdkCallTerminationReason)],
            modifiers: null)
        ?? throw new InvalidOperationException("SDK CallStateChangedEventArgs ctor signature changed.");

    // Same story for the DTMF payload: the SDK keeps its ctor internal, so the test builds it
    // reflectively to drive the adapter through the SDK's real event contract.
    private static readonly ConstructorInfo DtmfArgsCtor =
        typeof(SdkDtmfReceivedEventArgs).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            types: [typeof(SdkDtmfTone), typeof(int), typeof(NativeCall)],
            modifiers: null)
        ?? throw new InvalidOperationException("SDK DtmfReceivedEventArgs ctor signature changed.");

    public SdkCallId CallId { get; init; } = SdkCallId.New();

    public SdkCallState State { get; set; } = SdkCallState.Idle;

    public SdkCallDirection Direction { get; init; } = SdkCallDirection.Outbound;

    public string RemoteParty { get; init; } = "sip:peer@example.com";

    public SdkCallTerminationReason? TerminationReason { get; set; }

    public CallActionResult RejectResult { get; init; } = CallActionResult.Success();

    public bool AcceptCalled { get; private set; }

    public bool HangupCalled { get; private set; }

    public bool RejectCalled { get; private set; }

    /// <summary>Status the last reject carried. Matters for the drain path, which must not use 486.</summary>
    public int RejectStatusCode { get; private set; }

    public SdkDtmfTone? LastDtmf { get; private set; }

    public bool HasStateChangedSubscribers => StateChanged is not null;

    public bool HasDtmfReceivedSubscribers => DtmfReceived is not null;

    public event EventHandler<SdkCallStateChangedEventArgs>? StateChanged;

    public event EventHandler<SdkDtmfReceivedEventArgs>? DtmfReceived;

#pragma warning disable CS0067 // Interface members the adapter never observes.
    public event EventHandler<CalloraVoipSdk.Core.Domain.Events.HoldStateChangedEventArgs>? HoldStateChanged;
    public event EventHandler<CalloraVoipSdk.Core.Domain.Events.TransferRequestedEventArgs>? TransferRequested;
    public event EventHandler<CalloraVoipSdk.Core.Domain.Events.CallQualitySnapshotChangedEventArgs>? QualitySnapshotChanged;
    public event EventHandler<CalloraVoipSdk.Core.Domain.Events.CallIceConnectionStateChangedEventArgs>? IceConnectionStateChanged;
#pragma warning restore CS0067

    public void RaiseStateChanged(SdkCallState oldState, SdkCallState newState)
    {
        State = newState;
        var args = (SdkCallStateChangedEventArgs)StateArgsCtor.Invoke([oldState, newState, this, TerminationReason]);
        StateChanged?.Invoke(this, args);
    }

    public void RaiseDtmfReceived(char symbol, int durationMs)
    {
        var args = (SdkDtmfReceivedEventArgs)DtmfArgsCtor.Invoke([new SdkDtmfTone(symbol), durationMs, this]);
        DtmfReceived?.Invoke(this, args);
    }

    public Task AcceptAsync(CancellationToken ct = default)
    {
        AcceptCalled = true;
        return Task.CompletedTask;
    }

    public Task HangupAsync(CancellationToken ct = default)
    {
        HangupCalled = true;
        return Task.CompletedTask;
    }

    public Task SendDtmfAsync(SdkDtmfTone tone, CancellationToken ct = default)
    {
        LastDtmf = tone;
        return Task.CompletedTask;
    }

    public Task<CallActionResult> RejectAsync(int statusCode = 486, string? reasonPhrase = null, CancellationToken ct = default)
    {
        RejectCalled = true;
        RejectStatusCode = statusCode;
        return Task.FromResult(RejectResult);
    }

    // ── Members the adapter must never touch ────────────────────────────────────
    public DateTimeOffset StartedAt => throw new NotSupportedException();

    public CalloraVoipSdk.Core.Domain.Lines.IPhoneLine Line => throw new NotSupportedException();

    public CalloraVoipSdk.Core.Domain.Calls.CallMediaParameters? MediaParameters => throw new NotSupportedException();

    public CalloraVoipSdk.Core.Domain.Calls.CallQualitySnapshot QualitySnapshot => throw new NotSupportedException();

    public CalloraVoipSdk.Core.Domain.Calls.CallRtpStatistics? RtpStatistics => throw new NotSupportedException();

    public CalloraVoipSdk.Core.Domain.Calls.CallIceSnapshot? IceSnapshot => throw new NotSupportedException();

    public CalloraVoipSdk.Core.Domain.Calls.CallIceState IceConnectionState => throw new NotSupportedException();

    public string? RemoteAssertedIdentity => AssertedIdentity;

    public string? Diversion { get; init; }

    public string? AssertedIdentity { get; init; }

    public string? CalledNumber { get; init; }

    public string? RemoteNumber { get; init; }

    public string? RemoteDisplayName { get; init; }

    public string? LocalParty { get; init; }

    public Task HoldAsync(CancellationToken ct = default) => throw new NotSupportedException();

    public Task UnholdAsync(CancellationToken ct = default) => throw new NotSupportedException();

    public Task RestartIceAsync(CancellationToken ct = default) => throw new NotSupportedException();

    public Task BlindTransferAsync(string targetUri, CancellationToken ct = default) => throw new NotSupportedException();

    public Task<bool> AttendedTransferAsync(NativeCall consultationCall, CancellationToken ct = default) =>
        throw new NotSupportedException();

    public Task<CallActionResult> RedirectAsync(IReadOnlyList<string> contactUris, int statusCode = 302, CancellationToken ct = default) =>
        throw new NotSupportedException();

    public Task<CallActionResult> SendInfoAsync(string contentType, string body, CancellationToken ct = default) =>
        throw new NotSupportedException();

    public Task<CallActionResult> SendOptionsAsync(CancellationToken ct = default) => throw new NotSupportedException();

    public Task<CallActionResult> SendSubscribeAsync(string eventType, int expiresSeconds = 300, string? acceptHeader = null, string? body = null, CancellationToken ct = default) =>
        throw new NotSupportedException();

    public Task<CallActionResult> SendNotifyAsync(string eventType, string subscriptionState, string? contentType = null, string? body = null, CancellationToken ct = default) =>
        throw new NotSupportedException();
}

/// <summary>A sender that fails on attach, to prove <see cref="SdkCall.OpenAudioAsync"/> disposes the tap.</summary>
internal sealed class ThrowingOnAttachSender : IMediaSender
{
    public bool Disposed { get; private set; }

    public void AttachToCall(NativeCall call) => throw new InvalidOperationException("attach failed");

    public void Detach()
    {
    }

    public Task SendAsync(MediaFrame frame, CancellationToken ct = default) => Task.CompletedTask;

    public void Dispose() => Disposed = true;
}
