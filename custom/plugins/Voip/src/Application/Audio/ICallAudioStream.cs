namespace Callora.Plugins.Voip.Application.Audio;

/// <summary>
/// Bidirectional audio stream of one connected call, obtained via
/// <see cref="ICall.OpenAudioAsync"/>. Frames are encoded in
/// <see cref="Format"/>; disposing the stream stops frame delivery without
/// affecting the call.
/// </summary>
public interface ICallAudioStream : IAsyncDisposable
{
    /// <summary>Encoding of all frames flowing over this stream.</summary>
    AudioFormat Format { get; }

    /// <summary>
    /// Raised for every inbound audio frame. Handlers run synchronously on the
    /// channel's media path: they must not block and must not perform I/O
    /// inline — buffer into a queue and return immediately.
    /// </summary>
    event EventHandler<AudioFrameReceivedEventArgs>? FrameReceived;

    /// <summary>
    /// Sends one encoded audio frame to the remote party. Frames sent while
    /// the call is not <see cref="CallState.Connected"/> are dropped.
    /// </summary>
    Task SendAsync(AudioFrame frame, CancellationToken cancellationToken = default);
}
