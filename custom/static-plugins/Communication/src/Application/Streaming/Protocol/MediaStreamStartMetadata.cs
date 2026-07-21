namespace Callora.Plugin.Communication.Application.Streaming.Protocol;

/// <summary>
/// Metadata carried by the opening <see cref="MediaStreamEventType.Start"/> frame so the consumer
/// knows which call it is attached to and how the audio is encoded (Twilio's <c>start.mediaFormat</c>).
/// </summary>
/// <param name="SessionId">The media-stream session id.</param>
/// <param name="CallId">The call the stream is bound to.</param>
/// <param name="Encoding">Audio encoding label (for example: <c>audio/x-mulaw</c>).</param>
/// <param name="SampleRateHz">Audio sample rate in Hz.</param>
public sealed record MediaStreamStartMetadata(
    string SessionId,
    string CallId,
    string Encoding,
    int SampleRateHz);
