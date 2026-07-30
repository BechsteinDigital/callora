using System.Collections.Generic;
using Callora.Plugin.Communication.Application.RealtimeMedia;

namespace Callora.Core.Tests.Communication.Conference;

/// <summary>
/// A hand-written <see cref="IRealtimeMediaProvider"/> double for the conference SFU tests: mints a fresh
/// <see cref="FakeMediaPeer"/> per <see cref="CreatePeer"/> and records them in join order, so a test can
/// reach the peer created for the n-th participant and drive its events.
/// </summary>
internal sealed class FakeRealtimeMediaProvider : IRealtimeMediaProvider
{
    private readonly List<FakeMediaPeer> _createdPeers = [];

    /// <summary>The peers minted, in creation (join) order.</summary>
    public IReadOnlyList<FakeMediaPeer> CreatedPeers => _createdPeers;

    public IMediaPeer CreatePeer(MediaPeerOptions options)
    {
        var peer = new FakeMediaPeer();
        _createdPeers.Add(peer);
        return peer;
    }
}
