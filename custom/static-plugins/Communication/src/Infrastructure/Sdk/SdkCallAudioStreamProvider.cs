using System.Collections.Concurrent;
using Callora.Plugin.Communication.Abstractions;
using Callora.Plugin.Communication.Application.Streaming;

namespace Callora.Plugin.Communication.Infrastructure.Sdk;

/// <summary>
/// The real <see cref="ICallAudioStreamProvider"/> backing the WebSocket media surface: a
/// thread-safe map of <c>callId → live audio stream</c>. The SDK call tracker (B4-deep-2) registers
/// a stream when a call reaches Connected and removes it when the call ends; the WS handler resolves
/// it via <see cref="OpenAsync"/>. Replaces <c>NoCallAudioStreamProvider</c> once wired.
/// </summary>
public sealed class SdkCallAudioStreamProvider : ICallAudioStreamProvider
{
    private readonly ConcurrentDictionary<string, ICallAudioStream> _byCallId = new(StringComparer.Ordinal);

    /// <summary>Registers the live audio stream of a connected call.</summary>
    public void Register(string callId, ICallAudioStream stream)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(callId);
        ArgumentNullException.ThrowIfNull(stream);
        _byCallId[callId] = stream;
    }

    /// <summary>Removes a call's stream (on call end); returns it so the caller can dispose it.</summary>
    public ICallAudioStream? Remove(string callId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(callId);
        return _byCallId.TryRemove(callId, out var stream) ? stream : null;
    }

    /// <inheritdoc />
    public Task<ICallAudioStream?> OpenAsync(string callId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_byCallId.TryGetValue(callId, out var stream) ? stream : null);
}
