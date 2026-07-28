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
    private readonly List<string> _addedCandidates = [];

    public PeerConnectionState State { get; set; } = PeerConnectionState.New;

    public int DisposeCount { get; private set; }

    public IReadOnlyList<byte> SentDtmf => _sentDtmf;

    public bool HasStateChangedSubscribers => ConnectionStateChanged is not null;

    // ── Signalling recorders (S3) ───────────────────────────────────────────────
    /// <summary>The offer SDP handed back by <see cref="CreateOffer"/>.</summary>
    public string OfferSdp { get; set; } = "v=0-offer";

    /// <summary>Set once <see cref="CreateOffer"/> is called.</summary>
    public bool OfferCreated { get; private set; }

    /// <summary>The remote SDP applied via <see cref="SetRemoteDescriptionAsync"/>, if any.</summary>
    public string? RemoteDescription { get; private set; }

    /// <summary>Set once <see cref="StartAsync"/> is called.</summary>
    public bool Started { get; private set; }

    /// <summary>Set once <see cref="GatherCandidatesAsync"/> is called.</summary>
    public bool CandidatesGathered { get; private set; }

    /// <summary>
    /// Records the order of key lifecycle calls — "offer", "gather", "start" — so tests can assert
    /// that STUN gathering happens after the offer and before the transport starts.
    /// </summary>
    public List<string> CallOrder { get; } = [];

    /// <summary>Remote ICE candidates applied via <see cref="AddIceCandidateAsync"/>, in order.</summary>
    public IReadOnlyList<string> AddedCandidates => _addedCandidates;

    public event EventHandler<PeerConnectionState>? ConnectionStateChanged;
    public event EventHandler<string>? LocalIceCandidateDiscovered;

#pragma warning disable CS0067 // Events the adapters never observe.
    public event EventHandler<RemoteTrack>? TrackReceived;
    public event EventHandler<DtmfTone>? DtmfReceived;
    public event EventHandler? VideoKeyFrameRequested;
#pragma warning restore CS0067

    /// <summary>Sets <see cref="State"/> and raises the lifecycle event with the new state.</summary>
    public void RaiseStateChanged(PeerConnectionState newState)
    {
        State = newState;
        ConnectionStateChanged?.Invoke(this, newState);
    }

    /// <summary>Raises a locally discovered ICE candidate (RFC 8838 trickle) for the signalling tests.</summary>
    public void RaiseLocalIceCandidate(string candidate) =>
        LocalIceCandidateDiscovered?.Invoke(this, candidate);

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

    /// <summary>A local ICE candidate raised at offer time (SDK surfaces the host candidate then), if set.</summary>
    public string? CandidateOnOffer { get; set; }

    public string CreateOffer()
    {
        OfferCreated = true;
        CallOrder.Add("offer");
        if (CandidateOnOffer is not null)
        {
            RaiseLocalIceCandidate(CandidateOnOffer);
        }

        return OfferSdp;
    }

    public Task AddIceCandidateAsync(string candidate, CancellationToken cancellationToken = default)
    {
        _addedCandidates.Add(candidate);
        return Task.CompletedTask;
    }

    /// <summary>Hook run right after a remote description is applied (lets a test drive the peer state).</summary>
    public Action? OnRemoteDescriptionApplied { get; set; }

    public Task<string> SetRemoteDescriptionAsync(string remoteSdp, CancellationToken cancellationToken = default)
    {
        RemoteDescription = remoteSdp;
        OnRemoteDescriptionApplied?.Invoke();
        // Offerer applying the peer's answer: the SDK returns the local offer unchanged.
        return Task.FromResult(OfferSdp);
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        Started = true;
        CallOrder.Add("start");
        return Task.CompletedTask;
    }

    // ── Members neither adapter nor signalling handler touch ─────────────────────
    public string? LocalDescription => throw new NotSupportedException();

    public IPEndPoint? LocalMediaEndPoint => throw new NotSupportedException();

    public Task GatherCandidatesAsync(CancellationToken cancellationToken = default)
    {
        CandidatesGathered = true;
        CallOrder.Add("gather");
        return Task.CompletedTask;
    }

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
