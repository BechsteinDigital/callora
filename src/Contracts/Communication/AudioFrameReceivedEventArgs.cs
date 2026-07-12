namespace Callora.Contracts.Communication;

/// <summary>
/// Payload of <see cref="ICallAudioStream.FrameReceived"/>.
/// </summary>
public sealed class AudioFrameReceivedEventArgs : EventArgs
{
    public AudioFrameReceivedEventArgs(AudioFrame frame)
    {
        Frame = frame;
    }

    /// <summary>The received encoded audio frame.</summary>
    public AudioFrame Frame { get; }
}
