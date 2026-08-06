using Callora.Plugin.Communication.Application.Conference;
using CalloraVoipSdk.Audio.Abstractions.Processing;

namespace Callora.Plugin.Communication.Infrastructure.RealtimeMedia;

/// <summary>
/// Adapts one SDK <see cref="IAudioPayloadCodec"/> to the neutral <see cref="IAudioTranscoder"/> port,
/// so the conference bridge mixes without seeing an SDK type (ADR-016).
/// </summary>
internal sealed class SdkAudioTranscoder : IAudioTranscoder
{
    private readonly IAudioPayloadCodec _codec;

    /// <summary>Wraps one SDK payload codec instance, which it then owns.</summary>
    public SdkAudioTranscoder(IAudioPayloadCodec codec)
    {
        ArgumentNullException.ThrowIfNull(codec);
        _codec = codec;
    }

    /// <inheritdoc />
    public int PcmSampleRate => _codec.PcmSampleRate;

    /// <inheritdoc />
    public byte[] DecodeToPcm16(ReadOnlySpan<byte> payload) => _codec.DecodeToPcm16(payload);

    /// <inheritdoc />
    public byte[] EncodeFromPcm16(ReadOnlySpan<byte> pcm16) => _codec.EncodeFromPcm16(pcm16);

    /// <inheritdoc />
    public void Dispose() => _codec.Dispose();
}
