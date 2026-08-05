namespace Callora.Plugin.Communication.Application.Streaming;

/// <summary>
/// Mints the one-time ticket a consumer needs to open a media socket for a live call. This is the
/// only production path that creates a <see cref="Domain.Streaming.MediaStreamSession"/> (#114);
/// without it the WebSocket surface exists but nothing can legitimately reach it.
/// </summary>
public interface IMediaStreamSessionMinter
{
    /// <summary>
    /// Mints a ticket for the call named in <paramref name="command"/>, or returns
    /// <see langword="null"/> when that workspace has no such live call. Ownership is checked
    /// against live call tracking, so a ticket can never be minted for another workspace's call
    /// or for a conversation that is already over.
    /// </summary>
    Task<MediaStreamTicket?> MintAsync(MintMediaStreamCommand command, CancellationToken cancellationToken = default);
}
