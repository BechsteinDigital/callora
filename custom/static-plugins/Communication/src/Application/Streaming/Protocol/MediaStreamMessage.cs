namespace Callora.Plugin.Communication.Application.Streaming.Protocol;

/// <summary>
/// One decoded frame of the media-stream protocol. Only the fields relevant to the
/// <see cref="Event"/> are populated: <see cref="Payload"/> for <see cref="MediaStreamEventType.Media"/>,
/// <see cref="MarkName"/> for <see cref="MediaStreamEventType.Mark"/>, <see cref="Start"/> for
/// <see cref="MediaStreamEventType.Start"/>.
/// </summary>
/// <param name="Event">The protocol event.</param>
/// <param name="Payload">Base64-encoded audio payload (media frames only).</param>
/// <param name="MarkName">Marker name (mark frames only).</param>
/// <param name="Start">Opening metadata (start frames only).</param>
public sealed record MediaStreamMessage(
    MediaStreamEventType Event,
    string? Payload = null,
    string? MarkName = null,
    MediaStreamStartMetadata? Start = null)
{
    /// <summary>An audio frame carrying a base64 payload.</summary>
    public static MediaStreamMessage Media(string base64Payload) =>
        new(MediaStreamEventType.Media, Payload: base64Payload);

    /// <summary>The opening frame for a stream.</summary>
    public static MediaStreamMessage ForStart(MediaStreamStartMetadata start) =>
        new(MediaStreamEventType.Start, Start: start);

    /// <summary>A named playback marker.</summary>
    public static MediaStreamMessage ForMark(string name) =>
        new(MediaStreamEventType.Mark, MarkName: name);

    /// <summary>The closing frame.</summary>
    public static MediaStreamMessage Stop { get; } = new(MediaStreamEventType.Stop);

    /// <summary>A barge-in flush request.</summary>
    public static MediaStreamMessage Clear { get; } = new(MediaStreamEventType.Clear);
}
