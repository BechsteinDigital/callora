using Callora.Plugin.Communication.Application.RealtimeMedia;

namespace Callora.Plugin.Communication.Application.Conference;

/// <summary>
/// An outbound track that accepts frames and drops them — for media an endpoint cannot render. It
/// exists so the forwarding path can add the tracks it always adds without asking whether this
/// particular participant has any use for them.
/// </summary>
internal sealed class DiscardingMediaOutboundTrack : IMediaOutboundTrack
{
    /// <inheritdoc />
    public Task SendFrameAsync(MediaFrame frame, CancellationToken ct = default) => Task.CompletedTask;
}
