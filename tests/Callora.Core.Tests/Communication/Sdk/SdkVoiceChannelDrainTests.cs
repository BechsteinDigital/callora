using Callora.Plugin.Communication.Abstractions;
using Callora.Plugin.Communication.Application.Voice;
using Callora.Plugin.Communication.Infrastructure.Sdk;
using CalloraVoipSdk.Core.Application.Media;
using Xunit;
using SdkCallState = CalloraVoipSdk.Core.Domain.Calls.CallState;

namespace Callora.Core.Tests.Communication.Sdk;

/// <summary>
/// Quiescing is the SIP half of a drain (ADR-018 §2.1): the line withdraws its registration so the
/// carrier stops routing here, calls already up are left alone, and anything that still arrives is
/// turned away in a way the carrier can act on.
/// </summary>
public sealed class SdkVoiceChannelDrainTests
{
    [Fact]
    public async Task QuiescingWithdrawsTheRegistration()
    {
        var line = new FakePhoneLine();
        var channel = NewChannel(line);

        await channel.QuiesceAsync();

        // This is what actually stops the traffic. Rejecting call after call would only treat the
        // symptom, and only after the caller already heard ringing.
        Assert.Equal(1, line.UnregisterCalls);
    }

    [Fact]
    public async Task QuiescingTwiceWithdrawsOnce()
    {
        var line = new FakePhoneLine();
        var channel = NewChannel(line);

        await channel.QuiesceAsync();
        await channel.QuiesceAsync();

        Assert.Equal(1, line.UnregisterCalls);
    }

    [Fact]
    public async Task AnInboundCallDuringTheDrainIsRefusedWithServiceUnavailable()
    {
        var line = new FakePhoneLine();
        var channel = NewChannel(line);
        var delivered = 0;
        channel.IncomingCall += (_, _) => delivered++;
        await channel.QuiesceAsync();

        var arriving = new FakeSdkCall { State = SdkCallState.Ringing };
        line.RaiseIncomingCall(arriving);

        // 503, not the SDK's default 486: busy tells the carrier the person is occupied, service
        // unavailable tells it to try the next route in the trunk group.
        Assert.True(arriving.RejectCalled);
        Assert.Equal(503, arriving.RejectStatusCode);
        Assert.Equal(0, delivered);
    }

    [Fact]
    public async Task AnOutboundCallDuringTheDrainIsRefused()
    {
        var line = new FakePhoneLine { DialResult = new FakeSdkCall { State = SdkCallState.Idle } };
        var channel = NewChannel(line);
        await channel.QuiesceAsync();

        // A drain refuses in both directions; dialling out of a withdrawn registration would fail at
        // the carrier anyway, and later.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            channel.PlaceCallAsync(new CallTarget("sip:a@example.com")));
    }

    [Fact]
    public async Task CallsAlreadyUpAreNotHungUp()
    {
        var line = new FakePhoneLine();
        var channel = NewChannel(line);
        channel.IncomingCall += (_, _) => { };
        var established = new FakeSdkCall { State = SdkCallState.Connected };
        line.RaiseIncomingCall(established);

        await channel.QuiesceAsync();

        // The entire point of draining: the conversation gets to finish by itself.
        Assert.False(established.HangupCalled);
        Assert.Equal(1, channel.ActiveCalls);
    }

    [Fact]
    public async Task AFailedUnregisterStillLeavesTheChannelQuiesced()
    {
        var line = new FakePhoneLine { UnregisterFailure = new InvalidOperationException("registrar unreachable.") };
        var channel = NewChannel(line);

        await channel.QuiesceAsync();

        // A registrar we cannot reach must not leave the channel accepting work: the refusal is
        // local, so it holds even when the withdrawal did not go through.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            channel.PlaceCallAsync(new CallTarget("sip:a@example.com")));
    }

    private static (IMediaReceiver Receiver, IMediaSender Sender) Tap() =>
        (new FakeMediaReceiver(), new FakeMediaSender());

    private static SdkVoiceChannel NewChannel(FakePhoneLine line) =>
        new("ch", "Line", "plugin", line, Tap, maxConcurrentCalls: 10);
}
