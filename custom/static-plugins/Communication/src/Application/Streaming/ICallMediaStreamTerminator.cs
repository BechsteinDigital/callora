namespace Callora.Plugin.Communication.Application.Streaming;

/// <summary>
/// Ends every media stream bound to a call. Call control depends on this narrow port rather than on
/// the streaming stack, so a deployment without the media surface simply has no terminator and the
/// call lifecycle is unaffected.
/// </summary>
/// <remarks>
/// A stream must not outlive the conversation it carries (#114): once the call is over there is no
/// audio left to bridge, and an unspent ticket for it would still be redeemable. Implementations are
/// total — they report failures instead of propagating them, because a media-teardown problem must
/// never leave a call unfinalized.
/// </remarks>
public interface ICallMediaStreamTerminator
{
    /// <summary>Closes the persisted sessions of the call and aborts its live sockets.</summary>
    Task CloseForCallAsync(string workspaceKey, string callId, CancellationToken cancellationToken = default);
}
