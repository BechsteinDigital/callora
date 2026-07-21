namespace Callora.Plugin.Communication.Abstractions;

/// <summary>Audio-Codec eines Call-Streams. v1: G.711 (µ-law/A-law) für SIP/PSTN.</summary>
public enum AudioCodec
{
    /// <summary>G.711 µ-law (PCMU), 8 kHz.</summary>
    G711Ulaw = 0,

    /// <summary>G.711 A-law (PCMA), 8 kHz.</summary>
    G711Alaw = 1
}
