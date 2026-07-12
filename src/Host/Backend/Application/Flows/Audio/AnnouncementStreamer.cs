using Callora.Contracts.Communication;

namespace Callora.Host.Backend.Application.Flows.Audio;

/// <summary>
/// Streams a PCM16 mono WAV announcement into a call audio stream: encodes to
/// the negotiated G.711 codec and paces 20 ms frames in real time. The first
/// production consumer of ICall.OpenAudioAsync.
/// </summary>
public static class AnnouncementStreamer
{
    public static async Task StreamAsync(
        ICallAudioStream audioStream,
        byte[] wavBytes,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(audioStream);

        var aLaw = string.Equals(audioStream.Format.Codec, "PCMA", StringComparison.OrdinalIgnoreCase);
        var muLaw = string.Equals(audioStream.Format.Codec, "PCMU", StringComparison.OrdinalIgnoreCase);
        if (!aLaw && !muLaw)
        {
            throw new InvalidOperationException(
                $"Announcement playback supports PCMA/PCMU calls; the call negotiated '{audioStream.Format.Codec}'.");
        }

        var (samples, sampleRate) = PcmWaveReader.Read(wavBytes);
        if (sampleRate != audioStream.Format.ClockRate)
        {
            throw new InvalidOperationException(
                $"Announcement sample rate {sampleRate} Hz does not match the call clock rate " +
                $"{audioStream.Format.ClockRate} Hz — export the file accordingly.");
        }

        var samplesPerFrame = sampleRate / 50; // 20 ms
        var frameDuration = TimeSpan.FromMilliseconds(20);

        for (var position = 0; position < samples.Length; position += samplesPerFrame)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var length = Math.Min(samplesPerFrame, samples.Length - position);
            var encoded = G711Codec.Encode(samples.AsSpan(position, length), aLaw);
            await audioStream
                .SendAsync(new AudioFrame(encoded, frameDuration), cancellationToken)
                .ConfigureAwait(false);
            await Task.Delay(frameDuration, cancellationToken).ConfigureAwait(false);
        }
    }
}
