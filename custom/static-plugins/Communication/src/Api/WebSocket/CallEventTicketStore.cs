using System.Buffers.Text;
using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace Callora.Plugin.Communication.Api.WebSocket;

/// <summary>
/// Mints and consumes the single-use tickets that authorize one call-event socket (#116). Purely
/// in-memory: a ticket authorizes a connection to this process, and a process that restarts has none
/// outstanding.
/// </summary>
/// <remarks>
/// The same shape as the media and signalling tickets: high-entropy token, short window, redeemable
/// once. It carries only a workspace — the stream is a filtered view of that workspace's calls, not a
/// handle on any one of them, so there is nothing else to bind.
/// </remarks>
public sealed class CallEventTicketStore(TimeProvider timeProvider, TimeSpan ticketTimeToLive)
{
    private const int TokenEntropyBytes = 32;

    private readonly ConcurrentDictionary<string, CallEventTicket> _tickets = new(StringComparer.Ordinal);

    /// <summary>Mints a ticket for the workspace and returns its token.</summary>
    public string Mint(string workspaceKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceKey);

        // Lazy sweep before inserting, so tickets minted and never redeemed cannot accumulate.
        var cutoff = timeProvider.GetUtcNow() - ticketTimeToLive;
        foreach (var entry in _tickets)
        {
            if (entry.Value.MintedAt < cutoff)
            {
                _tickets.TryRemove(entry.Key, out _);
            }
        }

        var token = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(TokenEntropyBytes));
        _tickets[token] = new CallEventTicket(workspaceKey, timeProvider.GetUtcNow());
        return token;
    }

    /// <summary>How long a minted ticket stays redeemable — advisory for the client, enforced here.</summary>
    public TimeSpan TicketTimeToLive => ticketTimeToLive;

    /// <summary>
    /// Redeems a ticket, returning its workspace, or <see langword="null"/> when the token is unknown,
    /// expired or already used. Removal is the single-use gate: only one caller can take the entry.
    /// </summary>
    public string? TryConsume(string token)
    {
        if (string.IsNullOrWhiteSpace(token) || !_tickets.TryRemove(token, out var ticket))
        {
            return null;
        }

        return timeProvider.GetUtcNow() - ticket.MintedAt <= ticketTimeToLive ? ticket.WorkspaceKey : null;
    }
}
