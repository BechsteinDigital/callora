namespace Callora.Plugin.Communication.Application.Conference;

/// <summary>
/// The audio payload codecs the conference bridge transcodes between, named neutrally so the mixing
/// layer never sees a media SDK type (ADR-016).
/// </summary>
internal enum ConferenceAudioCodec
{
    /// <summary>Opus (RFC 7587) — what browser participants negotiate.</summary>
    Opus,

    /// <summary>G.711 µ-law (PCMU, payload type 0) — what a telephone leg carries.</summary>
    G711Ulaw,

    /// <summary>G.711 A-law (PCMA, payload type 8).</summary>
    G711Alaw,

    /// <summary>G.722 wide-band (payload type 9).</summary>
    G722,
}
