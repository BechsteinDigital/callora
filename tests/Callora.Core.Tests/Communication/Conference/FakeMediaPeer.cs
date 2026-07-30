using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Callora.Plugin.Communication.Abstractions.RealtimeMedia;
using Callora.Plugin.Communication.Application.RealtimeMedia;

namespace Callora.Core.Tests.Communication.Conference;

/// <summary>
/// A hand-written <see cref="IMediaPeer"/> double for the conference SFU tests. Records what the router and
/// the participant session drive through it — added outbound tracks, produced offers, applied answers and
/// candidates, gather/start/key-frame calls — and can raise the peer's events (a remote track, a downstream
/// PLI, a local candidate, a connection-state change) so the whole SFU choreography is observable without a
/// real media stack. Each <see cref="CreateOffer"/> returns a distinct SDP so a test can tell the initial
/// offer from a renegotiation re-offer.
/// </summary>
internal sealed class FakeMediaPeer : IMediaPeer
{
    private readonly List<FakeMediaOutboundTrack> _outboundTracks = [];
    private readonly List<SessionDescription> _appliedAnswers = [];
    private readonly List<IceCandidate> _appliedCandidates = [];
    private int _offerCounter;

    public MediaConnectionState ConnectionState { get; private set; } = MediaConnectionState.New;

    // ── Recorders ────────────────────────────────────────────────────────────────
    public int CreateOfferCount { get; private set; }
    public int GatherCount { get; private set; }
    public int StartCount { get; private set; }
    public int KeyFrameRequestCount { get; private set; }
    public int DisposeCount { get; private set; }

    public IReadOnlyList<FakeMediaOutboundTrack> OutboundTracks => _outboundTracks;
    public IReadOnlyList<SessionDescription> AppliedAnswers => _appliedAnswers;
    public IReadOnlyList<IceCandidate> AppliedCandidates => _appliedCandidates;

    /// <summary>Optional hook invoked inside <see cref="CreateOffer"/> — lets a test surface a local
    /// candidate during offer creation (after the session subscribed, before the offer is signalled) to
    /// exercise the trickle-gate buffering window.</summary>
    public Action? OnCreateOffer { get; set; }

    public bool HasStateChangedSubscribers => ConnectionStateChanged is not null;
    public bool HasLocalIceCandidateSubscribers => LocalIceCandidateDiscovered is not null;
    public bool HasTrackReceivedSubscribers => RemoteTrackReceived is not null;
    public bool HasKeyFrameSubscribers => KeyFrameRequested is not null;

    public event EventHandler<MediaConnectionState>? ConnectionStateChanged;
    public event EventHandler<IceCandidate>? LocalIceCandidateDiscovered;
    public event EventHandler<IRemoteMediaTrack>? RemoteTrackReceived;
    public event EventHandler? KeyFrameRequested;

    /// <summary>Finds the outbound track this peer holds for a given source and kind (null when absent).</summary>
    public FakeMediaOutboundTrack? OutboundFor(string streamId, MediaTrackKind kind) =>
        _outboundTracks.Find(t => t.StreamId == streamId && t.Kind == kind);

    // ── Event drivers ────────────────────────────────────────────────────────────
    public void RaiseRemoteTrackReceived(IRemoteMediaTrack track) => RemoteTrackReceived?.Invoke(this, track);

    public void RaiseKeyFrameRequested() => KeyFrameRequested?.Invoke(this, EventArgs.Empty);

    public void RaiseLocalIceCandidate(IceCandidate candidate) =>
        LocalIceCandidateDiscovered?.Invoke(this, candidate);

    public void RaiseConnectionStateChanged(MediaConnectionState state)
    {
        ConnectionState = state;
        ConnectionStateChanged?.Invoke(this, state);
    }

    // ── IMediaPeer ───────────────────────────────────────────────────────────────
    public SessionDescription CreateOffer()
    {
        CreateOfferCount++;
        OnCreateOffer?.Invoke();
        return new SessionDescription("offer", $"v=0-offer-{++_offerCounter}");
    }

    public Task<SessionDescription?> ApplyRemoteDescriptionAsync(SessionDescription remote, CancellationToken ct = default)
    {
        _appliedAnswers.Add(remote);
        return Task.FromResult<SessionDescription?>(null);
    }

    public Task AddIceCandidateAsync(IceCandidate candidate, CancellationToken ct = default)
    {
        _appliedCandidates.Add(candidate);
        return Task.CompletedTask;
    }

    public Task GatherCandidatesAsync(CancellationToken ct = default)
    {
        GatherCount++;
        return Task.CompletedTask;
    }

    public Task StartAsync(CancellationToken ct = default)
    {
        StartCount++;
        return Task.CompletedTask;
    }

    public IMediaOutboundTrack AddOutboundTrack(MediaTrackKind kind, string streamId)
    {
        var track = new FakeMediaOutboundTrack(kind, streamId);
        _outboundTracks.Add(track);
        return track;
    }

    public ValueTask<bool> RequestKeyFrameAsync(CancellationToken ct = default)
    {
        KeyFrameRequestCount++;
        return ValueTask.FromResult(true);
    }

    public ValueTask DisposeAsync()
    {
        DisposeCount++;
        return ValueTask.CompletedTask;
    }
}
