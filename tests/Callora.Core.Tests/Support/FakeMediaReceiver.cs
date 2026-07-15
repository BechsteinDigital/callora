using CalloraVoipSdk.Core.Application.Media;
using SdkCall = CalloraVoipSdk.Core.Domain.Calls.ICall;

namespace Callora.Core.Tests.Support;

/// <summary>
/// SDK media receiver fake for audio-stream adapter tests; no RTP involved.
/// </summary>
public sealed class FakeMediaReceiver : IMediaReceiver
{
    public SdkCall? AttachedCall { get; private set; }

    public bool IsDisposed { get; private set; }

    public event EventHandler<MediaFrameReceivedEventArgs>? FrameReceived;

    public void AttachToCall(SdkCall call) => AttachedCall = call;

    public void Detach() => AttachedCall = null;

    public void RaiseFrame(MediaFrame frame) =>
        FrameReceived?.Invoke(this, new MediaFrameReceivedEventArgs(frame));

    public void Dispose() => IsDisposed = true;
}
