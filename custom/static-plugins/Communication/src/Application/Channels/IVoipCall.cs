using Callora.Plugin.Communication.Abstractions;
using Callora.Plugin.Communication.Application.Audio;

namespace Callora.Plugin.Communication.Application.Channels;

/// <summary>
/// The voice plugin's own call type: the modality-neutral <see cref="ICall"/>
/// plus voice-media access. Media (audio) is voip-specific and therefore lives
/// here, not on the shared contract — a future video plugin exposes its own
/// media surface (REV2 §10.1 C, ADR-012).
/// </summary>
public interface IVoipCall : ICall
{
    /// <summary>
    /// Opens the bidirectional audio stream of the call. Requires
    /// <see cref="CallState.Connected"/> — throws
    /// <see cref="InvalidOperationException"/> otherwise. Multiple streams can
    /// be open in parallel; each observes every inbound frame.
    /// </summary>
    Task<ICallAudioStream> OpenAudioAsync(CancellationToken cancellationToken = default);
}
