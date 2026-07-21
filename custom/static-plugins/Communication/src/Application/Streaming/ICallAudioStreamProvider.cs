using Callora.Plugin.Communication.Abstractions;

namespace Callora.Plugin.Communication.Application.Streaming;

/// <summary>
/// Resolves the live duplex audio stream of a call so the media bridge can attach a WebSocket
/// consumer to it. The real implementation (B4-deep) opens the CalloraVoipSdk call's
/// <see cref="IVoipCall.OpenAudioAsync"/>; until a call runtime exists the default returns
/// <see langword="null"/> and the bridge closes the stream cleanly.
/// </summary>
public interface ICallAudioStreamProvider
{
    /// <summary>Opens the audio stream of a live call, or returns <see langword="null"/> when there is none.</summary>
    Task<ICallAudioStream?> OpenAsync(string callId, CancellationToken cancellationToken = default);
}
