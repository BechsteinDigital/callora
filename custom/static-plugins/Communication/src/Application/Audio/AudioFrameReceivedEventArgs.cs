namespace Callora.Plugin.Communication.Application.Audio;

/// <summary>
/// Payload of <see cref="ICallAudioStream.FrameReceived"/>.
/// </summary>
public sealed class AudioFrameReceivedEventArgs : EventArgs
{
    /// <summary>Creates the payload for one received frame.</summary>
    public AudioFrameReceivedEventArgs(AudioFrame frame)
    {
        Frame = frame;
    }

    /// <summary>The received encoded audio frame.</summary>
    public AudioFrame Frame { get; }
}
