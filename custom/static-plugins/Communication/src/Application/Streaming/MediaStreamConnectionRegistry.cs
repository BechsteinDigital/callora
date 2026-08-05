using System.Collections.Concurrent;

namespace Callora.Plugin.Communication.Application.Streaming;

/// <summary>
/// Tracks the live media sockets of this host by call, so ending a call can abort them (#114).
/// Purely in-memory runtime state: a socket belongs to the process that accepted it, and a process
/// that restarts has none left to abort.
/// </summary>
/// <remarks>
/// The registry hands out a registration handle instead of an unregister method, so a handler that
/// returns — normally or by exception — always leaves the registry clean.
/// </remarks>
public sealed class MediaStreamConnectionRegistry
{
    // callId → sessionId → the socket's abort signal. Two levels because a call can carry several
    // consumer streams (a listener and a duplex agent), and each has to be releasable on its own.
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, CancellationTokenSource>> _byCall =
        new(StringComparer.Ordinal);

    /// <summary>
    /// Registers a live socket for the call. Disposing the returned handle removes it; the caller
    /// keeps ownership of <paramref name="abort"/>.
    /// </summary>
    public MediaStreamConnectionRegistration Register(string callId, string sessionId, CancellationTokenSource abort)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(callId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentNullException.ThrowIfNull(abort);

        var sessions = _byCall.GetOrAdd(callId, _ => new ConcurrentDictionary<string, CancellationTokenSource>(StringComparer.Ordinal));
        sessions[sessionId] = abort;
        return new MediaStreamConnectionRegistration(this, callId, sessionId);
    }

    /// <summary>
    /// Aborts every socket registered for the call and returns how many were signalled. Cancelling
    /// an already-disposed source is not an error here — the socket ended on its own a moment
    /// earlier, which is the outcome this method wants.
    /// </summary>
    public int AbortForCall(string callId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(callId);

        if (!_byCall.TryRemove(callId, out var sessions))
        {
            return 0;
        }

        var aborted = 0;
        foreach (var abort in sessions.Values)
        {
            try
            {
                abort.Cancel();
                aborted++;
            }
            catch (ObjectDisposedException)
            {
                // The handler already tore its socket down; nothing left to signal.
            }
        }

        return aborted;
    }

    /// <summary>Removes one registration. Called by the handle; safe when the call entry is already gone.</summary>
    internal void Unregister(string callId, string sessionId)
    {
        if (!_byCall.TryGetValue(callId, out var sessions))
        {
            return;
        }

        sessions.TryRemove(sessionId, out _);
        if (sessions.IsEmpty)
        {
            // Best effort: a concurrent Register may have just re-added the call, in which case the
            // removal below loses the race and the entry stays — correct, it is in use again.
            _byCall.TryRemove(new KeyValuePair<string, ConcurrentDictionary<string, CancellationTokenSource>>(callId, sessions));
        }
    }
}
