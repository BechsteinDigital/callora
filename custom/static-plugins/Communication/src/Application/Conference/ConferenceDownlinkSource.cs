namespace Callora.Plugin.Communication.Application.Conference;

/// <summary>
/// One conference participant as seen by a downlink mix: its own decoder — the compressing codecs
/// carry state across frames, so every participant needs its own — and the frame waiting to be mixed.
/// </summary>
internal sealed class ConferenceDownlinkSource : IDisposable
{
    /// <summary>Creates the source over the decoder it owns.</summary>
    public ConferenceDownlinkSource(IAudioTranscoder decoder)
    {
        ArgumentNullException.ThrowIfNull(decoder);
        Decoder = decoder;
    }

    /// <summary>This participant's decoder, for the whole lifetime of its stream.</summary>
    public IAudioTranscoder Decoder { get; }

    /// <summary>Decoded PCM16 awaiting the next outbound frame; <see langword="null"/> once consumed.</summary>
    public byte[]? Pending { get; set; }

    /// <inheritdoc />
    public void Dispose() => Decoder.Dispose();
}
