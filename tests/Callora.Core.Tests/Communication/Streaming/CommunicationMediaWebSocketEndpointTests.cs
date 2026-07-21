using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Callora.Administration.Api;
using Callora.Core.Application.Plugins;
using Callora.Core.Application.Plugins.Contracts;
using Callora.Core.Tests.Support;
using Callora.Plugin.Communication.Abstractions;
using Callora.Plugin.Communication.Api.WebSocket;
using Callora.Plugin.Communication.Application.Streaming;
using Callora.Plugin.Communication.Application.Streaming.Protocol;
using Callora.Plugin.Communication.Domain.Streaming;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Callora.Core.Tests.Communication.Streaming;

/// <summary>
/// End-to-end media surface over the real Host-WS foundation (B4a-2): a valid connect token
/// upgrades and audio round-trips through the bridge (consumer <c>media</c> echoed back by a
/// loopback call), while unknown, expired and already-used tokens are rejected before the upgrade.
/// </summary>
public sealed class CommunicationMediaWebSocketEndpointTests
{
    private static readonly Uri GoodUri = new("ws://localhost/ws/communication/media/good-token");

    [Fact]
    public async Task ValidToken_Upgrades_AndAudioRoundTripsThroughLoopback()
    {
        var store = new InMemoryMediaStreamSessionStore();
        await store.AddAsync(NewPending("good-token", DateTimeOffset.UtcNow));
        await using var app = await CreateAppAsync(store, new LoopbackCallAudioStreamProvider());
        var socket = await app.GetTestServer().CreateWebSocketClient().ConnectAsync(GoodUri, CancellationToken.None);

        // The bridge opens with a start frame carrying the negotiated format.
        var start = await ReceiveMessageAsync(socket);
        Assert.Equal(MediaStreamEventType.Start, start!.Event);

        // Consumer sends audio; the loopback call echoes it, so it comes back as a media frame.
        await SendMessageAsync(socket, MediaStreamMessage.Media(Convert.ToBase64String(new byte[] { 7, 7, 7 })));
        var echoed = await ReceiveUntilAsync(socket, MediaStreamEventType.Media);

        Assert.NotNull(echoed);
        Assert.Equal(new byte[] { 7, 7, 7 }, Convert.FromBase64String(echoed!.Payload!));
    }

    [Fact]
    public async Task UnknownToken_IsRejected()
    {
        var store = new InMemoryMediaStreamSessionStore();
        await using var app = await CreateAppAsync(store, new LoopbackCallAudioStreamProvider());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            app.GetTestServer().CreateWebSocketClient().ConnectAsync(GoodUri, CancellationToken.None));
    }

    [Fact]
    public async Task ExpiredToken_IsRejected()
    {
        var store = new InMemoryMediaStreamSessionStore();
        await store.AddAsync(NewPending("good-token", DateTimeOffset.UtcNow.AddMinutes(-10)));
        await using var app = await CreateAppAsync(store, new LoopbackCallAudioStreamProvider());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            app.GetTestServer().CreateWebSocketClient().ConnectAsync(GoodUri, CancellationToken.None));
    }

    [Fact]
    public async Task AlreadyUsedToken_IsRejected()
    {
        var store = new InMemoryMediaStreamSessionStore();
        var used = NewPending("good-token", DateTimeOffset.UtcNow);
        used.Activate(DateTimeOffset.UtcNow); // token already consumed
        await store.AddAsync(used);
        await using var app = await CreateAppAsync(store, new LoopbackCallAudioStreamProvider());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            app.GetTestServer().CreateWebSocketClient().ConnectAsync(GoodUri, CancellationToken.None));
    }

    private static MediaStreamSession NewPending(string token, DateTimeOffset createdAt) => new(
        "sess-1", "call-1", "ws-a", "ai-agent", token,
        AudioFormat.G711Ulaw8k20ms, MediaStreamDirection.Bidirectional, createdAt);

    private static async Task<WebApplication> CreateAppAsync(
        IMediaStreamSessionStore store,
        ICallAudioStreamProvider provider)
    {
        var contributor = new CommunicationMediaWebSocketContributor(store, provider);

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton<ICalloraPluginCatalog>(new StaticPluginCatalog(new Dictionary<Type, IReadOnlyList<object>>
        {
            [typeof(IHostWebSocketEndpointContributor)] = [contributor]
        }));

        var app = builder.Build();
        app.UseWebSockets();
        app.MapPluginWebSocketEndpoints();
        await app.StartAsync();
        return app;
    }

    private static async Task SendMessageAsync(WebSocket socket, MediaStreamMessage message)
    {
        var bytes = Encoding.UTF8.GetBytes(MediaStreamMessageCodec.Encode(message));
        await socket.SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, CancellationToken.None);
    }

    private static async Task<MediaStreamMessage?> ReceiveMessageAsync(WebSocket socket)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var buffer = new byte[4096];
        var result = await socket.ReceiveAsync(buffer, timeout.Token);
        if (result.MessageType == WebSocketMessageType.Close)
        {
            return null;
        }

        return MediaStreamMessageCodec.TryDecode(Encoding.UTF8.GetString(buffer, 0, result.Count));
    }

    private static async Task<MediaStreamMessage?> ReceiveUntilAsync(WebSocket socket, MediaStreamEventType eventType)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var buffer = new byte[4096];
        while (!timeout.IsCancellationRequested)
        {
            var result = await socket.ReceiveAsync(buffer, timeout.Token);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                return null;
            }

            var message = MediaStreamMessageCodec.TryDecode(Encoding.UTF8.GetString(buffer, 0, result.Count));
            if (message?.Event == eventType)
            {
                return message;
            }
        }

        return null;
    }
}

/// <summary>Reference-semantics in-memory <see cref="IMediaStreamSessionStore"/> for endpoint tests.</summary>
internal sealed class InMemoryMediaStreamSessionStore : IMediaStreamSessionStore
{
    private readonly ConcurrentDictionary<string, MediaStreamSession> _byId = new();

    public Task AddAsync(MediaStreamSession session, CancellationToken cancellationToken = default)
    {
        _byId[session.Id] = session;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(MediaStreamSession session, CancellationToken cancellationToken = default)
    {
        _byId[session.Id] = session;
        return Task.CompletedTask;
    }

    public Task<MediaStreamSession?> GetByConnectTokenAsync(string connectToken, CancellationToken cancellationToken = default)
    {
        foreach (var session in _byId.Values)
        {
            if (session.ConnectToken == connectToken)
            {
                return Task.FromResult<MediaStreamSession?>(session);
            }
        }

        return Task.FromResult<MediaStreamSession?>(null);
    }

    public Task<MediaStreamSession?> GetAsync(string workspaceKey, string sessionId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_byId.TryGetValue(sessionId, out var session) && session.WorkspaceKey == workspaceKey ? session : null);

    public Task<int> DeleteByWorkspaceAsync(string workspaceKey, CancellationToken cancellationToken = default)
    {
        var removed = 0;
        foreach (var pair in _byId)
        {
            if (pair.Value.WorkspaceKey == workspaceKey && _byId.TryRemove(pair.Key, out _))
            {
                removed++;
            }
        }

        return Task.FromResult(removed);
    }
}

/// <summary>Provider returning a loopback stream whose <c>SendAsync</c> echoes back as inbound audio.</summary>
internal sealed class LoopbackCallAudioStreamProvider : ICallAudioStreamProvider
{
    public Task<ICallAudioStream?> OpenAsync(string callId, CancellationToken cancellationToken = default) =>
        Task.FromResult<ICallAudioStream?>(new LoopbackCallAudioStream());
}

internal sealed class LoopbackCallAudioStream : ICallAudioStream
{
    public AudioFormat Format { get; } = AudioFormat.G711Ulaw8k20ms;

    public event EventHandler<AudioFrameReceivedEventArgs>? FrameReceived;

    public ValueTask SendAsync(ReadOnlyMemory<byte> frame, CancellationToken cancellationToken = default)
    {
        FrameReceived?.Invoke(this, new AudioFrameReceivedEventArgs(frame.ToArray()));
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
