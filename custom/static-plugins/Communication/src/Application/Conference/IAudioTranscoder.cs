namespace Callora.Plugin.Communication.Application.Conference;

/// <summary>
/// Transcodes between one encoded audio payload format and PCM16 little-endian, in a single stream
/// direction. The neutral port the conference bridge mixes over; an adapter binds it to a media SDK.
/// </summary>
/// <remarks>
/// <b>One instance, one direction.</b> The compressing codecs carry predictor state across frames, so
/// an instance belongs to exactly one stream of one participant — sharing it between two directions or
/// two participants interleaves their state and produces artefacts that are hard to trace back here.
/// </remarks>
internal interface IAudioTranscoder : IDisposable
{
    /// <summary>The PCM16 sample rate this instance decodes to and encodes from, in Hz. Not the RTP clock.</summary>
    int PcmSampleRate { get; }

    /// <summary>Decodes one encoded payload into PCM16 little-endian bytes.</summary>
    byte[] DecodeToPcm16(ReadOnlySpan<byte> payload);

    /// <summary>Encodes PCM16 little-endian bytes into one encoded payload.</summary>
    byte[] EncodeFromPcm16(ReadOnlySpan<byte> pcm16);
}
