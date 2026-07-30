using System.Collections.Generic;
using System.Threading.Tasks;
using CalloraVoipSdk.WebRtc;

namespace Callora.Core.Tests.Communication.RealtimeMedia;

/// <summary>
/// A hand-written <see cref="IWebRtcClient"/> double. Hands out queued <see cref="FakeSdkPeerConnection"/>
/// instances so a provider test can assert <c>CreatePeer</c> delegation and disposal.
/// </summary>
internal sealed class FakeSdkWebRtcClient : IWebRtcClient
{
    private readonly Queue<FakeSdkPeerConnection> _peers = new();

    public int CreatePeerCallCount { get; private set; }

    public bool DisposeAsyncCalled { get; private set; }

    public void EnqueuePeer(FakeSdkPeerConnection peer) => _peers.Enqueue(peer);

    public IPeerConnection CreatePeer()
    {
        CreatePeerCallCount++;
        return _peers.Count > 0 ? _peers.Dequeue() : new FakeSdkPeerConnection();
    }

    public IPeerConnectionManager Peers => throw new System.NotSupportedException();

    public IWebRtcModuleRegistry Modules => throw new System.NotSupportedException();

    public ValueTask DisposeAsync()
    {
        DisposeAsyncCalled = true;
        return ValueTask.CompletedTask;
    }
}
