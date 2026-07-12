using Callora.Contracts.Communication;

namespace Callora.Host.Backend.Tests.Support;

/// <summary>
/// Contract-only audio stream fake recording sent frames; tests raise inbound
/// frames via <see cref="RaiseFrameReceived"/>.
/// </summary>
public sealed class RecordingCallAudioStream : ICallAudioStream
{
    private readonly List<AudioFrame> _sentFrames = [];

    public RecordingCallAudioStream(AudioFormat? format = null)
    {
        Format = format ?? new AudioFormat("PCMU", 8000);
    }

    public AudioFormat Format { get; }

    public IReadOnlyList<AudioFrame> SentFrames => _sentFrames;

    public bool IsDisposed { get; private set; }

    public event EventHandler<AudioFrameReceivedEventArgs>? FrameReceived;

    public Task SendAsync(AudioFrame frame, CancellationToken cancellationToken = default)
    {
        _sentFrames.Add(frame);
        return Task.CompletedTask;
    }

    public void RaiseFrameReceived(AudioFrame frame) =>
        FrameReceived?.Invoke(this, new AudioFrameReceivedEventArgs(frame));

    public ValueTask DisposeAsync()
    {
        IsDisposed = true;
        return ValueTask.CompletedTask;
    }
}
