using Microsoft.Extensions.Logging;

namespace Callora.Plugin.Communication.Application.Streaming;

/// <summary>
/// Default <see cref="ICallMediaStreamTerminator"/>: closes the call's persisted sessions and aborts
/// the sockets this host holds for it.
/// </summary>
/// <remarks>
/// Both halves are needed and neither substitutes for the other. Closing the rows invalidates
/// unspent tickets — a ticket for a call that has ended must not still open a socket. Aborting the
/// sockets stops streams that are already running, which no database write reaches. The persisted
/// half runs first: it is the durable one, and it is what a second host observes.
/// </remarks>
public sealed class CallMediaStreamTerminator(
    IMediaStreamSessionStore sessionStore,
    MediaStreamConnectionRegistry connections,
    TimeProvider timeProvider,
    ILogger<CallMediaStreamTerminator> logger) : ICallMediaStreamTerminator
{
    /// <inheritdoc />
    public async Task CloseForCallAsync(string workspaceKey, string callId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(callId);

        var closed = 0;
        try
        {
            closed = await sessionStore
                .CloseByCallAsync(workspaceKey, callId, timeProvider.GetUtcNow(), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // A call must finalize even when its media bookkeeping fails; the sockets are still
            // aborted below, which is the part that matters for the live conversation.
            logger.LogWarning(ex, "Closing media sessions of call {CallId} in workspace {WorkspaceKey} failed.", callId, workspaceKey);
        }

        var aborted = connections.AbortForCall(callId);
        if (closed > 0 || aborted > 0)
        {
            logger.LogInformation(
                "Call {CallId} ended: closed {ClosedCount} media session(s) and aborted {AbortedCount} live socket(s).",
                callId,
                closed,
                aborted);
        }
    }
}
