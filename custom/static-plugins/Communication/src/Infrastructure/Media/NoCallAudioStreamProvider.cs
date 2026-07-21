using Callora.Plugin.Communication.Abstractions;
using Callora.Plugin.Communication.Application.Streaming;

namespace Callora.Plugin.Communication.Infrastructure.Media;

/// <summary>
/// Default audio provider until a call runtime exists (B5/B4-deep): there is no live call to
/// attach to, so it opens no stream. The media handler closes the WebSocket cleanly when this
/// returns <see langword="null"/>.
/// </summary>
public sealed class NoCallAudioStreamProvider : ICallAudioStreamProvider
{
    /// <inheritdoc />
    public Task<ICallAudioStream?> OpenAsync(string callId, CancellationToken cancellationToken = default) =>
        Task.FromResult<ICallAudioStream?>(null);
}
