using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using CalloraVoipSdk.WebRtc;

namespace Callora.Core.Tests.Communication.Sdk;

/// <summary>
/// A hand-written <see cref="IPeerConnection"/> double for the WebRTC adapter tests. Only the members the
/// adapter touches are functional (state, the lifecycle event, DTMF, and async dispose as hangup);
/// everything else throws, documenting that the adapter must not depend on it.
/// </summary>
internal sealed class FakePeerConnection : IPeerConnection
{
    private readonly List<byte> _sentDtmf = [];

    public PeerConnectionState State { get; set; } = PeerConnectionState.New;

    public int DisposeCount { get; private set; }

    public IReadOnlyList<byte> SentDtmf => _sentDtmf;

    public bool HasStateChangedSubscribers => ConnectionStateChanged is not null;

    public event EventHandler<PeerConnectionState>? ConnectionStateChanged;

#pragma warning disable CS0067 // Events the adapter never observes.
    public event EventHandler<RemoteTrack>? TrackReceived;
    public event EventHandler<string>? LocalIceCandidateDiscovered;
    public event EventHandler<DtmfTone>? DtmfReceived;
    public event EventHandler? VideoKeyFrameRequested;
#pragma warning restore CS0067

    /// <summary>Sets <see cref="State"/> and raises the lifecycle event with the new state.</summary>
    public void RaiseStateChanged(PeerConnectionState newState)
    {
        State = newState;
        ConnectionStateChanged?.Invoke(this, newState);
    }

    public Task SendDtmfAsync(byte toneCode, int durationMs = 160, CancellationToken cancellationToken = default)
    {
        _sentDtmf.Add(toneCode);
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        DisposeCount++;
        return ValueTask.CompletedTask;
    }

    // ── Members the adapter must never touch ────────────────────────────────────
    public string? LocalDescription => throw new NotSupportedException();

    public IPEndPoint? LocalMediaEndPoint => throw new NotSupportedException();

    public string CreateOffer() => throw new NotSupportedException();

    public Task AddIceCandidateAsync(string candidate, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<string> SetRemoteDescriptionAsync(string remoteSdp, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task GatherCandidatesAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task StartAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public ValueTask SendAudioAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task SendVideoFrameAsync(ReadOnlyMemory<byte> encodedFrame, uint rtpTimestamp, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task SendVideoFrameAsync(string rid, ReadOnlyMemory<byte> encodedFrame, uint rtpTimestamp, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public IDisposable AttachMediaTap(IMediaTap tap) => throw new NotSupportedException();

    public WebRtcStats GetStats() => throw new NotSupportedException();
}

/// <summary>
/// A hand-written <see cref="IWebRtcClient"/> double. The channel only reads liveness from it and never
/// creates peers itself in v1 (the signalling path does), so <see cref="CreatePeer"/> hands out queued
/// fakes and the registry members throw.
/// </summary>
internal sealed class FakeWebRtcClient : IWebRtcClient
{
    private readonly Queue<FakePeerConnection> _peers = new();

    public bool DisposeAsyncCalled { get; private set; }

    /// <summary>Queues a peer to be returned by the next <see cref="CreatePeer"/> call.</summary>
    public void EnqueuePeer(FakePeerConnection peer) => _peers.Enqueue(peer);

    public IPeerConnection CreatePeer() =>
        _peers.Count > 0 ? _peers.Dequeue() : new FakePeerConnection();

    public IPeerConnectionManager Peers => throw new NotSupportedException();

    public IWebRtcModuleRegistry Modules => throw new NotSupportedException();

    public ValueTask DisposeAsync()
    {
        DisposeAsyncCalled = true;
        return ValueTask.CompletedTask;
    }
}
