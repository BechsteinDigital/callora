using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Callora.Core.Application.Plugins.Contracts;
using Callora.Plugin.Communication.Abstractions;
using Callora.Plugin.Communication.Api.WebSocket;
using Callora.Plugin.Communication.Application.Streaming;
using Callora.Plugin.Communication.Domain.Streaming;
using Xunit;

namespace Callora.Core.Tests.Communication.Streaming;

/// <summary>
/// S1 regression: <see cref="MediaStreamWebSocketHandler"/> must NOT dispose the call-scoped audio
/// stream when the WebSocket session ends. The stream is owned by the
/// <c>SdkCallAudioRegistrar</c> and must survive a consumer disconnecting while the call is live.
/// </summary>
public sealed class MediaStreamWebSocketHandlerTests
{
    [Fact]
    public async Task HandlerEnd_DoesNotDispose_SharedCallStream()
    {
        // Arrange: a session store with one live session and a provider returning a tracking stream.
        var store = new SingleSessionStore(workspaceKey: "ws-1", sessionId: "sess-1", callId: "call-1");
        var trackingStream = new DisposeCountingCallAudioStream();
        var provider = new FixedCallAudioStreamProvider(trackingStream);

        var handler = new MediaStreamWebSocketHandler(store, provider);

        // A close-immediately fake socket: ReceiveAsync returns Close right away so the bridge's
        // consumer-to-call pump exits, causing MediaBridge.RunAsync to terminate without the
        // handler ever touching the stream's dispose path.
        var socket = new CloseImmediatelyWebSocket();
        var request = new HostWebSocketConnectRequest(
            "communication", "media", new Dictionary<string, string>(),
            new Dictionary<string, string[]>(), []);
        var connection = new HostWebSocketConnection(socket, request, subject: "ws-1/sess-1");

        // Act
        await handler.HandleAsync(connection);

        // Assert: the shared, call-scoped stream must not have been disposed by the handler.
        Assert.Equal(0, trackingStream.DisposeCount);
    }
}

// ── Fakes ────────────────────────────────────────────────────────────────────

/// <summary>Session store that serves one hardcoded active session.</summary>
internal sealed class SingleSessionStore : IMediaStreamSessionStore
{
    private readonly string _workspaceKey;
    private readonly string _sessionId;
    private readonly string _callId;

    public SingleSessionStore(string workspaceKey, string sessionId, string callId)
    {
        _workspaceKey = workspaceKey;
        _sessionId = sessionId;
        _callId = callId;
    }

    public Task<MediaStreamSession?> GetAsync(string workspaceKey, string sessionId, CancellationToken cancellationToken = default)
    {
        if (workspaceKey == _workspaceKey && sessionId == _sessionId)
        {
            var session = new MediaStreamSession(
                _sessionId, _callId, _workspaceKey, "ai-agent", "tok",
                AudioFormat.G711Ulaw8k20ms, MediaStreamDirection.Bidirectional, DateTimeOffset.UtcNow);
            session.Activate(DateTimeOffset.UtcNow);
            return Task.FromResult<MediaStreamSession?>(session);
        }

        return Task.FromResult<MediaStreamSession?>(null);
    }

    public Task AddAsync(MediaStreamSession session, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task UpdateAsync(MediaStreamSession session, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<MediaStreamSession?> GetByConnectTokenAsync(string connectToken, CancellationToken cancellationToken = default) =>
        Task.FromResult<MediaStreamSession?>(null);

    public Task<MediaStreamSession?> TryActivateByConnectTokenAsync(string connectToken, DateTimeOffset now, TimeSpan timeToLive, CancellationToken cancellationToken = default) =>
        Task.FromResult<MediaStreamSession?>(null);

    public Task<int> DeleteByWorkspaceAsync(string workspaceKey, CancellationToken cancellationToken = default) =>
        Task.FromResult(0);

    public Task<int> PurgeExpiredAsync(
        DateTimeOffset now, TimeSpan retention, CancellationToken cancellationToken = default) =>
        Task.FromResult(0);
}

/// <summary>Provider that always returns the same stream instance.</summary>
internal sealed class FixedCallAudioStreamProvider : ICallAudioStreamProvider
{
    private readonly ICallAudioStream _stream;

    public FixedCallAudioStreamProvider(ICallAudioStream stream) => _stream = stream;

    public Task<ICallAudioStream?> OpenAsync(string callId, CancellationToken cancellationToken = default) =>
        Task.FromResult<ICallAudioStream?>(_stream);
}

/// <summary>
/// Audio stream that counts how many times <see cref="DisposeAsync"/> is called.
/// Does not raise <see cref="FrameReceived"/> — the bridge sees no inbound audio but starts
/// and stays alive until the WebSocket closes.
/// </summary>
internal sealed class DisposeCountingCallAudioStream : ICallAudioStream
{
    private int _disposeCount;

    public int DisposeCount => _disposeCount;

    public AudioFormat Format { get; } = AudioFormat.G711Ulaw8k20ms;

#pragma warning disable CS0067
    public event EventHandler<AudioFrameReceivedEventArgs>? FrameReceived;
#pragma warning restore CS0067

    public ValueTask SendAsync(ReadOnlyMemory<byte> frame, CancellationToken cancellationToken = default) =>
        ValueTask.CompletedTask;

    public ValueTask DisposeAsync()
    {
        Interlocked.Increment(ref _disposeCount);
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// A <see cref="WebSocket"/> stub whose <see cref="ReceiveAsync"/> immediately returns a Close
/// frame, terminating any bridge read-loop on the first call after the start message is sent.
/// <see cref="SendAsync"/> is a no-op (records nothing). Used to drive the handler to completion
/// without a real network connection.
/// </summary>
internal sealed class CloseImmediatelyWebSocket : WebSocket
{
    private bool _closeSent;

    public override WebSocketState State => _closeSent ? WebSocketState.Closed : WebSocketState.Open;

    public override WebSocketCloseStatus? CloseStatus => _closeSent ? WebSocketCloseStatus.NormalClosure : null;

    public override string? CloseStatusDescription => null;

    public override string? SubProtocol => null;

    public override Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public override Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken)
    {
        _closeSent = true;
        return Task.FromResult(new WebSocketReceiveResult(0, WebSocketMessageType.Close, endOfMessage: true));
    }

    public override Task CloseAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public override Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public override void Abort() { }

    public override void Dispose() { }
}
