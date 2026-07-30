using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using CalloraVoipSdk.WebRtc;

namespace Callora.Core.Tests.Communication.RealtimeMedia;

/// <summary>
/// A hand-written CalloraVoipSdk <see cref="IPeerConnection"/> double for the neutral RealtimeMedia adapter
/// (M1). It records what the adapter delegates — added tracks, applied candidates, lifecycle calls, key-frame
/// requests — and can raise the peer's events so the adapter's mapping can be observed. Members the adapter
/// does not touch throw, documenting that it must not depend on them.
/// </summary>
internal sealed class FakeSdkPeerConnection : IPeerConnection
{
    private readonly List<string> _addedCandidates = [];
    private readonly List<FakeSdkVideoTrack> _videoTracks = [];
    private readonly List<FakeSdkAudioTrack> _audioTracks = [];

    public PeerConnectionState State { get; set; } = PeerConnectionState.New;

    // ── Recorders ────────────────────────────────────────────────────────────────
    public string OfferSdp { get; set; } = "v=0-offer";
    public int CreateOfferCount { get; private set; }
    public string? RemoteDescription { get; private set; }

    /// <summary>The SDP returned by <see cref="SetRemoteDescriptionAsync"/> — the local answer for an answerer,
    /// or the local offer unchanged for an offerer applying the peer's answer.</summary>
    public string ReturnedDescription { get; set; } = "v=0-answer";

    public int StartCount { get; private set; }
    public int GatherCount { get; private set; }
    public int DisposeCount { get; private set; }
    public int KeyFrameRequestCount { get; private set; }
    public bool KeyFrameRequestResult { get; set; } = true;

    public IReadOnlyList<string> AddedCandidates => _addedCandidates;
    public IReadOnlyList<FakeSdkVideoTrack> VideoTracks => _videoTracks;
    public IReadOnlyList<FakeSdkAudioTrack> AudioTracks => _audioTracks;

    public bool HasStateChangedSubscribers => ConnectionStateChanged is not null;
    public bool HasLocalIceCandidateSubscribers => LocalIceCandidateDiscovered is not null;
    public bool HasTrackReceivedSubscribers => TrackReceived is not null;
    public bool HasVideoKeyFrameSubscribers => VideoKeyFrameRequested is not null;

    public event EventHandler<PeerConnectionState>? ConnectionStateChanged;
    public event EventHandler<string>? LocalIceCandidateDiscovered;
    public event EventHandler<RemoteTrack>? TrackReceived;
    public event EventHandler? VideoKeyFrameRequested;

#pragma warning disable CS0067 // Events the adapter never observes.
    public event EventHandler<DtmfTone>? DtmfReceived;
    public event EventHandler<SignalingState>? SignalingStateChanged;
    public event EventHandler<BitrateRecommendation>? RecommendedBitrateChanged;
#pragma warning restore CS0067

    public void RaiseStateChanged(PeerConnectionState newState)
    {
        State = newState;
        ConnectionStateChanged?.Invoke(this, newState);
    }

    public void RaiseLocalIceCandidate(string candidate) =>
        LocalIceCandidateDiscovered?.Invoke(this, candidate);

    public void RaiseVideoKeyFrameRequested() => VideoKeyFrameRequested?.Invoke(this, EventArgs.Empty);

    public string CreateOffer()
    {
        CreateOfferCount++;
        return OfferSdp;
    }

    public Task<string> SetRemoteDescriptionAsync(string remoteSdp, CancellationToken cancellationToken = default)
    {
        RemoteDescription = remoteSdp;
        return Task.FromResult(ReturnedDescription);
    }

    public Task AddIceCandidateAsync(string candidate, CancellationToken cancellationToken = default)
    {
        _addedCandidates.Add(candidate);
        return Task.CompletedTask;
    }

    public Task GatherCandidatesAsync(CancellationToken cancellationToken = default)
    {
        GatherCount++;
        return Task.CompletedTask;
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        StartCount++;
        return Task.CompletedTask;
    }

    public IVideoTrack AddVideoTrack() => AddVideoTrack(new VideoTrackOptions());

    public IVideoTrack AddVideoTrack(VideoTrackOptions options)
    {
        var track = new FakeSdkVideoTrack((_videoTracks.Count + 1).ToString(), options);
        _videoTracks.Add(track);
        return track;
    }

    public IAudioTrack AddAudioTrack() => AddAudioTrack(new AudioTrackOptions());

    public IAudioTrack AddAudioTrack(AudioTrackOptions options)
    {
        var track = new FakeSdkAudioTrack((_audioTracks.Count + 1).ToString(), options);
        _audioTracks.Add(track);
        return track;
    }

    public ValueTask<bool> RequestVideoKeyFrameAsync(CancellationToken cancellationToken = default)
    {
        KeyFrameRequestCount++;
        return ValueTask.FromResult(KeyFrameRequestResult);
    }

    public ValueTask<bool> RequestVideoKeyFrameAsync(string mid, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("The neutral adapter requests key frames without a MID.");

    public ValueTask DisposeAsync()
    {
        DisposeCount++;
        return ValueTask.CompletedTask;
    }

    // ── Members the adapter does not touch ───────────────────────────────────────
    public SignalingState SignalingState => throw new NotSupportedException();
    public long? RecommendedOutgoingBitrateBps => throw new NotSupportedException();
    public string? LocalDescription => throw new NotSupportedException();
    public IPEndPoint? LocalMediaEndPoint => throw new NotSupportedException();
    public Task SendDtmfAsync(byte toneCode, int durationMs = 160, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
    public ValueTask SendAudioAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
    public Task SendVideoFrameAsync(ReadOnlyMemory<byte> encodedFrame, uint rtpTimestamp, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
    public Task SendVideoFrameAsync(string rid, ReadOnlyMemory<byte> encodedFrame, uint rtpTimestamp, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
    public IDisposable AttachMediaTap(IMediaTap tap) => throw new NotSupportedException();
    public WebRtcStats GetStats() => throw new NotSupportedException();
}
