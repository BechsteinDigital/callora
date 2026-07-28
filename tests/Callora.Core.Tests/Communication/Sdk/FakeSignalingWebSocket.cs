using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;

namespace Callora.Core.Tests.Communication.Sdk;

/// <summary>
/// An in-memory duplex <see cref="WebSocket"/> double for the WebRTC signalling handler tests. It records
/// every outbound text frame (as raw JSON) and serves a scripted queue of inbound text frames; once the
/// queue drains it yields a single <see cref="WebSocketMessageType.Close"/> so the handler's read loop
/// ends. Only the members the handler touches are functional; everything else throws.
/// </summary>
internal sealed class FakeSignalingWebSocket : WebSocket
{
    private readonly ConcurrentQueue<string> _sent = new();
    private readonly Queue<string> _inbound = new();
    private readonly SemaphoreSlim _inboundGate;
    private bool _closed;

    /// <param name="inboundFrames">The JSON text frames delivered to the handler, in order.</param>
    public FakeSignalingWebSocket(params string[] inboundFrames)
    {
        foreach (var frame in inboundFrames)
        {
            _inbound.Enqueue(frame);
        }

        _inboundGate = new SemaphoreSlim(0, int.MaxValue);
    }

    /// <summary>All outbound text frames, as raw JSON, in send order.</summary>
    public IReadOnlyList<string> Sent => _sent.ToArray();

    /// <summary>Every sent frame decoded to its <c>type</c> field (skipping unparseable frames).</summary>
    public IReadOnlyList<string> SentTypes
    {
        get
        {
            var types = new List<string>();
            foreach (var json in _sent)
            {
                using var document = JsonDocument.Parse(json);
                if (document.RootElement.TryGetProperty("type", out var type))
                {
                    types.Add(type.GetString()!);
                }
            }

            return types;
        }
    }

    public override Task SendAsync(
        ArraySegment<byte> buffer,
        WebSocketMessageType messageType,
        bool endOfMessage,
        CancellationToken cancellationToken)
    {
        _sent.Enqueue(Encoding.UTF8.GetString(buffer.Array!, buffer.Offset, buffer.Count));
        return Task.CompletedTask;
    }

    /// <summary>
    /// When <see langword="true"/>, <see cref="ReceiveAsync"/> blocks indefinitely (waiting for
    /// <paramref name="cancellationToken"/> to fire) once the inbound queue is drained, rather than
    /// returning a close frame. Use this to test deadline/cancellation paths.
    /// </summary>
    public bool BlockAfterQueueDrained { get; init; }

    public override async Task<WebSocketReceiveResult> ReceiveAsync(
        ArraySegment<byte> buffer,
        CancellationToken cancellationToken)
    {
        if (_inbound.Count == 0)
        {
            if (BlockAfterQueueDrained)
            {
                // Block until the caller's token fires (e.g. deadline or host cancel).
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
            }

            _closed = true;
            return new WebSocketReceiveResult(0, WebSocketMessageType.Close, endOfMessage: true);
        }

        var frame = _inbound.Dequeue();
        var bytes = Encoding.UTF8.GetBytes(frame);
        bytes.CopyTo(buffer.Array!, buffer.Offset);
        await Task.Yield();
        return new WebSocketReceiveResult(bytes.Length, WebSocketMessageType.Text, endOfMessage: true);
    }

    public override WebSocketCloseStatus? CloseStatus => _closed ? WebSocketCloseStatus.NormalClosure : null;
    public override string? CloseStatusDescription => null;
    public override WebSocketState State => _closed ? WebSocketState.Closed : WebSocketState.Open;
    public override string? SubProtocol => null;

    public override void Abort() { }

    public override Task CloseAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public override Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public override void Dispose() { }
}
