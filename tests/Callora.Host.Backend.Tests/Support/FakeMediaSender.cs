using CalloraVoipSdk.Core.Application.Media;
using SdkCall = CalloraVoipSdk.Core.Domain.Calls.ICall;

namespace Callora.Host.Backend.Tests.Support;

/// <summary>
/// SDK media sender fake recording sent frames; no RTP involved.
/// </summary>
public sealed class FakeMediaSender : IMediaSender
{
    private readonly List<MediaFrame> _sentFrames = [];

    public IReadOnlyList<MediaFrame> SentFrames => _sentFrames;

    public SdkCall? AttachedCall { get; private set; }

    public bool IsDisposed { get; private set; }

    public void AttachToCall(SdkCall call) => AttachedCall = call;

    public void Detach() => AttachedCall = null;

    public Task SendAsync(MediaFrame frame, CancellationToken ct = default)
    {
        _sentFrames.Add(frame);
        return Task.CompletedTask;
    }

    public void Dispose() => IsDisposed = true;
}
