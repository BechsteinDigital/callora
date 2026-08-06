namespace Callora.Plugin.Communication.Application.Conference;

/// <summary>
/// One frame going from a single-stream endpoint into the conference: the encoded payload and the RTP
/// timestamp it carries.
/// </summary>
/// <param name="Payload">The payload encoded in the conference's codec.</param>
/// <param name="RtpTimestamp">
/// The timestamp on the conference codec's RTP clock, which is not the PCM sample rate — for Opus it
/// runs at 48 kHz whatever rate the audio was coded at (RFC 7587 §4.1).
/// </param>
internal readonly record struct ConferenceUplinkFrame(byte[] Payload, uint RtpTimestamp);
