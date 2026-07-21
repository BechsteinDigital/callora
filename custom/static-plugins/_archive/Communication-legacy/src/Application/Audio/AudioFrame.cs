namespace Callora.Plugin.Communication.Application.Audio;

/// <summary>
/// One encoded audio frame on a <see cref="ICallAudioStream"/>. The payload is
/// encoded in the stream's <see cref="AudioFormat"/>.
/// </summary>
/// <param name="Payload">Encoded audio payload bytes.</param>
/// <param name="Duration">Playback duration of the frame, for example 20 ms.</param>
public readonly record struct AudioFrame(ReadOnlyMemory<byte> Payload, TimeSpan Duration);
