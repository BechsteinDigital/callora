using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Callora.Plugin.Communication.Abstractions;
using Callora.Plugin.Communication.Infrastructure.Sdk;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using SdkCallState = CalloraVoipSdk.Core.Domain.Calls.CallState;

namespace Callora.Core.Tests.Communication.Sdk;

/// <summary>
/// The audio-tracking channel decorator (B4-deep-2d): every call the inner channel surfaces — inbound
/// and outbound — is handed to the audio registrar (so its audio reaches the WS surface), while all
/// other members pass through and dispose tears down the inner channel.
/// </summary>
public sealed class AudioRegisteringChannelTests
{
    [Fact]
    public void Members_PassThroughToInner()
    {
        var inner = new FakeVoiceChannel
        {
            ChannelId = "ch-9",
            DisplayName = "Trunk",
            PluginId = "communication",
            Health = ChannelHealth.Degraded,
        };
        using var channel = new AudioRegisteringChannel(inner, NewRegistrar(out _));

        Assert.Equal("ch-9", channel.ChannelId);
        Assert.Equal("Trunk", channel.DisplayName);
        Assert.Equal("communication", channel.PluginId);
        Assert.Equal(ChannelHealth.Degraded, channel.Health);
        Assert.Contains(CommunicationCapabilities.Voice, channel.Capabilities);
    }

    [Fact]
    public async Task IncomingCall_TracksCall_AndReRaises()
    {
        var inner = new FakeVoiceChannel();
        using var channel = new AudioRegisteringChannel(inner, NewRegistrar(out var provider));
        var (call, sdk, _) = NewVoiceCall(SdkCallState.Ringing);
        IncomingCallEventArgs? reRaised = null;
        channel.IncomingCall += (_, e) => reRaised = e;

        inner.RaiseIncoming(call);
        sdk.RaiseStateChanged(SdkCallState.Ringing, SdkCallState.Connected);

        Assert.NotNull(reRaised); // re-raised to the decorator's consumers
        Assert.Same(call, reRaised!.Call);
        Assert.NotNull(await provider.OpenAsync(call.CallId)); // tracked → audio registered on connect
    }

    [Fact]
    public async Task PlaceCall_TracksResult()
    {
        var (call, sdk, _) = NewVoiceCall(SdkCallState.Dialing);
        var inner = new FakeVoiceChannel { NextPlaceResult = call };
        using var channel = new AudioRegisteringChannel(inner, NewRegistrar(out var provider));

        var placed = await channel.PlaceCallAsync(new CallTarget("sip:bob@example.com"));
        sdk.RaiseStateChanged(SdkCallState.Dialing, SdkCallState.Connected);

        Assert.Same(call, placed);
        Assert.Equal("sip:bob@example.com", inner.PlacedTarget!.Value);
        Assert.NotNull(await provider.OpenAsync(call.CallId)); // tracked → audio registered on connect
    }

    [Fact]
    public async Task NonVoiceCall_IsNotTracked_ButStillReRaised()
    {
        var inner = new FakeVoiceChannel();
        using var channel = new AudioRegisteringChannel(inner, NewRegistrar(out var provider));
        var plain = new FakePlainCall();
        var reRaised = false;
        channel.IncomingCall += (_, _) => reRaised = true;

        inner.RaiseIncoming(plain); // a bare ICall, not an IVoipCall

        Assert.True(reRaised);
        Assert.Null(await provider.OpenAsync(plain.CallId)); // no audio to track
    }

    [Fact]
    public void Dispose_UnsubscribesInner_AndDisposesInner()
    {
        var inner = new FakeVoiceChannel();
        var channel = new AudioRegisteringChannel(inner, NewRegistrar(out _));
        var reRaised = false;
        channel.IncomingCall += (_, _) => reRaised = true;

        channel.Dispose();
        inner.RaiseIncoming(new FakePlainCall());

        Assert.False(reRaised); // unsubscribed from inner
        Assert.False(inner.HasIncomingSubscribers);
        Assert.True(inner.Disposed);
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        var inner = new FakeVoiceChannel();
        var channel = new AudioRegisteringChannel(inner, NewRegistrar(out _));

        channel.Dispose();
        channel.Dispose(); // second dispose must be a no-op

        Assert.True(inner.Disposed);
    }

    [Fact]
    public void HealthChanged_ForwardsFromInnerChannel()
    {
        var inner = new FakeVoiceChannel();
        using var channel = new AudioRegisteringChannel(inner, NewRegistrar(out _));
        ChannelHealth? raised = null;
        channel.HealthChanged += (_, e) => raised = e.Health;

        inner.RaiseHealthChanged(ChannelHealth.Down);

        Assert.Equal(ChannelHealth.Down, raised); // decorator forwards the inner channel's health changes
    }

    private static SdkCallAudioRegistrar NewRegistrar(out SdkCallAudioStreamProvider provider)
    {
        provider = new SdkCallAudioStreamProvider();
        return new SdkCallAudioRegistrar(provider, NullLogger<SdkCallAudioRegistrar>.Instance);
    }

    private static (SdkCall Call, FakeSdkCall Sdk, FakeMediaReceiver Receiver) NewVoiceCall(SdkCallState initial)
    {
        var sdk = new FakeSdkCall { State = initial };
        var receiver = new FakeMediaReceiver();
        var call = new SdkCall(sdk, () => (receiver, new FakeMediaSender()));
        return (call, sdk, receiver);
    }
}

/// <summary>A controllable <see cref="IVoiceChannel"/> double for decorator/provisioner tests.</summary>
internal sealed class FakeVoiceChannel : IVoiceChannel, IDisposable
{
    public string ChannelId { get; init; } = "ch";

    public string DisplayName { get; init; } = "Fake";

    public string PluginId { get; init; } = "plugin";

    public IReadOnlyCollection<string> Capabilities { get; init; } = [CommunicationCapabilities.Voice];

    public ChannelHealth Health { get; set; } = ChannelHealth.Up;

    public bool Disposed { get; private set; }

    public CallTarget? PlacedTarget { get; private set; }

    public ICall? NextPlaceResult { get; set; }

    public bool HasIncomingSubscribers => IncomingCall is not null;

    public event EventHandler<ChannelHealthChangedEventArgs>? HealthChanged;

    public event EventHandler<IncomingCallEventArgs>? IncomingCall;

    public void RaiseIncoming(ICall call) => IncomingCall?.Invoke(this, new IncomingCallEventArgs(call));

    public void RaiseHealthChanged(ChannelHealth health) =>
        HealthChanged?.Invoke(this, new ChannelHealthChangedEventArgs(health));

    public Task<ICall> PlaceCallAsync(CallTarget target, CancellationToken cancellationToken = default)
    {
        PlacedTarget = target;
        return Task.FromResult(NextPlaceResult ?? throw new InvalidOperationException("NextPlaceResult not set."));
    }

    public void Dispose() => Disposed = true;
}

/// <summary>A bare <see cref="ICall"/> (not an <see cref="IVoipCall"/>) — must be re-raised but never tracked.</summary>
internal sealed class FakePlainCall : ICall
{
    public string CallId => "plain-1";

    public CallState State => CallState.Ringing;

    public CallDirection Direction => CallDirection.Inbound;

    public CallTarget Target => new("sip:x@example.com");

    public CallTerminationReason? TerminationReason => null;

    public event EventHandler<CallStateChangedEventArgs>? StateChanged
    {
        add { }
        remove { }
    }

    public Task AcceptAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task RejectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task HangupAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task SendDtmfAsync(char tone, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
