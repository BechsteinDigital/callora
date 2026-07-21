namespace Callora.Plugin.Communication.Abstractions;

/// <summary>
/// Ein eingehender Audio-Frame (inbound). Handler dürfen NICHT blockieren — den
/// Frame in eine Queue schreiben und sofort zurückkehren.
/// </summary>
/// <param name="frame">Der rohe kodierte Frame gemäß <see cref="ICallAudioStream.Format"/>.</param>
public sealed class AudioFrameReceivedEventArgs(ReadOnlyMemory<byte> frame) : EventArgs
{
    /// <summary>Der rohe kodierte Frame gemäß <see cref="ICallAudioStream.Format"/>.</summary>
    public ReadOnlyMemory<byte> Frame { get; } = frame;
}
