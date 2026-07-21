namespace Callora.Plugin.Communication.Application.Streaming.Protocol;

/// <summary>
/// The events of the Twilio-Media-Streams-style WebSocket protocol (§5.3). Server → consumer:
/// <see cref="Start"/>, <see cref="Media"/>, <see cref="Stop"/>. Consumer → server:
/// <see cref="Media"/>, <see cref="Clear"/> (barge-in). <see cref="Mark"/> flows both ways as a
/// playback marker.
/// </summary>
public enum MediaStreamEventType
{
    /// <summary>Opening frame carrying the session/call and negotiated audio format.</summary>
    Start,

    /// <summary>An audio frame (base64-encoded payload in the stream's format).</summary>
    Media,

    /// <summary>Closing frame; no more audio follows.</summary>
    Stop,

    /// <summary>A named playback marker (echoed back when reached).</summary>
    Mark,

    /// <summary>Consumer request to flush buffered outbound audio (barge-in).</summary>
    Clear
}
