using Callora.Plugin.Communication.Application.Conference;
using CalloraVoipSdk.Audio.Abstractions.Processing;

namespace Callora.Plugin.Communication.Infrastructure.RealtimeMedia;

/// <summary>
/// The <see cref="IAudioTranscoderFactory"/> over the SDK's public payload-codec surface — the single
/// place where the conference bridge's codec need meets a media SDK (ADR-016). It only maps the
/// neutral codec name onto the SDK's and passes the sample rate through; the SDK owns the codecs and
/// the argument validation, so no rate rule is restated (and able to drift) here.
/// </summary>
internal sealed class SdkAudioTranscoderFactory : IAudioTranscoderFactory
{
    /// <inheritdoc />
    public IAudioTranscoder Create(ConferenceAudioCodec codec, int pcmSampleRate) =>
        new SdkAudioTranscoder(AudioPayloadCodecFactory.Create(MapCodec(codec), pcmSampleRate));

    private static ActiveCodec MapCodec(ConferenceAudioCodec codec) => codec switch
    {
        ConferenceAudioCodec.Opus => ActiveCodec.Opus,
        ConferenceAudioCodec.G711Ulaw => ActiveCodec.Pcmu,
        ConferenceAudioCodec.G711Alaw => ActiveCodec.Pcma,
        ConferenceAudioCodec.G722 => ActiveCodec.G722,
        _ => throw new ArgumentOutOfRangeException(nameof(codec), codec, "Unknown conference audio codec."),
    };
}
