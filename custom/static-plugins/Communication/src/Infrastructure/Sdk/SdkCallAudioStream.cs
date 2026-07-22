using Callora.Plugin.Communication.Abstractions;
using CalloraVoipSdk.Core.Application.Media;

namespace Callora.Plugin.Communication.Infrastructure.Sdk;

/// <summary>
/// Adapts a CalloraVoipSdk call's media tap (<see cref="IMediaReceiver"/> / <see cref="IMediaSender"/>)
/// to the foundation's <see cref="ICallAudioStream"/>. Inbound SDK frames become
/// <see cref="ICallAudioStream.FrameReceived"/> events (the SDK payload is only valid during the
/// synchronous callback, so it is copied), and <see cref="SendAsync"/> forwards straight to the SDK
/// sender — the <c>MediaBridge</c> already paces the outbound direction at the frame interval, so no
/// second pacer is needed here. v1 audio is G.711 µ-law 8 kHz / 20 ms (PayloadType 0, 160 RTP units).
/// </summary>
public sealed class SdkCallAudioStream : ICallAudioStream
{
    private const int PcmuPayloadType = 0;
    private const uint FrameDurationRtpUnits = 160; // 20 ms at an 8 kHz RTP clock.

    private readonly IMediaReceiver _receiver;
    private readonly IMediaSender _sender;
    private int _disposed;

    /// <summary>Wraps a receiver/sender pair that are already attached to one call.</summary>
    public SdkCallAudioStream(IMediaReceiver receiver, IMediaSender sender)
    {
        ArgumentNullException.ThrowIfNull(receiver);
        ArgumentNullException.ThrowIfNull(sender);

        _receiver = receiver;
        _sender = sender;
        _receiver.FrameReceived += OnFrameReceived;
    }

    /// <inheritdoc />
    public AudioFormat Format => AudioFormat.G711Ulaw8k20ms;

    /// <inheritdoc />
    public event EventHandler<AudioFrameReceivedEventArgs>? FrameReceived;

    /// <inheritdoc />
    public ValueTask SendAsync(ReadOnlyMemory<byte> frame, CancellationToken cancellationToken = default) =>
        new(_sender.SendAsync(new MediaFrame(frame, PcmuPayloadType, FrameDurationRtpUnits), cancellationToken));

    private void OnFrameReceived(object? sender, MediaFrameReceivedEventArgs e)
    {
        var handler = FrameReceived;
        if (handler is null)
        {
            return;
        }

        // The SDK payload is only valid for the duration of this synchronous callback (Ownership-
        // Vertrag von AudioFrameReceivedEventArgs) — copy before handing it on.
        handler(this, new AudioFrameReceivedEventArgs(e.Frame.Payload.ToArray()));
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return ValueTask.CompletedTask;
        }

        _receiver.FrameReceived -= OnFrameReceived;
        _receiver.Detach();
        _sender.Detach();
        _receiver.Dispose();
        _sender.Dispose();
        return ValueTask.CompletedTask;
    }
}
