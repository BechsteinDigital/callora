namespace Callora.Plugin.Communication.Domain.Streaming;

/// <summary>Audio flow direction of a media stream relative to the external consumer.</summary>
public enum MediaStreamDirection
{
    /// <summary>Server → consumer only (the consumer listens).</summary>
    Inbound,

    /// <summary>Consumer → server only (the consumer speaks).</summary>
    Outbound,

    /// <summary>Both directions — a duplex voice agent.</summary>
    Bidirectional
}
