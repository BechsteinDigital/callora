using Callora.Plugin.Communication.Abstractions;
using Callora.Plugin.Communication.Application.Audio;
using CalloraVoipSdk.Core.Application.Media;

namespace Callora.Plugin.Communication.Application.Channels;

/// <summary>
/// Adapts one attached SDK media receiver/sender pair onto the platform audio
/// stream contract, translating frame durations between RTP clock units and
/// wall-clock time.
/// </summary>
public sealed class VoipSdkAudioStream : ICallAudioStream
{
    private readonly IMediaReceiver _receiver;
    private readonly IMediaSender _sender;
    private readonly int _payloadType;

    public VoipSdkAudioStream(
        IMediaReceiver receiver,
        IMediaSender sender,
        AudioFormat format,
        int payloadType)
    {
        ArgumentNullException.ThrowIfNull(receiver);
        ArgumentNullException.ThrowIfNull(sender);
        ArgumentNullException.ThrowIfNull(format);

        _receiver = receiver;
        _sender = sender;
        Format = format;
        _payloadType = payloadType;
        _receiver.FrameReceived += HandleFrameReceived;
    }

    public AudioFormat Format { get; }

    public event EventHandler<AudioFrameReceivedEventArgs>? FrameReceived;

    public Task SendAsync(AudioFrame frame, CancellationToken cancellationToken = default)
    {
        var durationRtpUnits = (uint)Math.Round(frame.Duration.TotalSeconds * Format.ClockRate);
        return _sender.SendAsync(
            new MediaFrame(frame.Payload, _payloadType, durationRtpUnits),
            cancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        _receiver.FrameReceived -= HandleFrameReceived;
        _receiver.Detach();
        _receiver.Dispose();
        _sender.Detach();
        _sender.Dispose();
        return ValueTask.CompletedTask;
    }

    private void HandleFrameReceived(object? sender, MediaFrameReceivedEventArgs args)
    {
        var duration = TimeSpan.FromSeconds(args.Frame.DurationRtpUnits / (double)Format.ClockRate);
        FrameReceived?.Invoke(this, new AudioFrameReceivedEventArgs(
            new AudioFrame(args.Frame.Payload, duration)));
    }
}
