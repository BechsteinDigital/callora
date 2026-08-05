namespace Callora.Core.Application.Surfaces;

/// <summary>
/// Storage for one-time handoff tickets (ADR-017 §8.4). Single use is a storage
/// property here rather than a check in calling code: redemption removes the row and
/// returns what it removed, so two concurrent redemptions cannot both succeed.
/// </summary>
public interface ISurfaceHandoffTicketStore
{
    /// <summary>Stores a newly minted ticket under the hash of its secret.</summary>
    /// <param name="ticket">The ticket to store.</param>
    /// <param name="tokenHash">Hash of the secret handed to the caller.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task CreateAsync(SurfaceHandoffTicket ticket, string tokenHash, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically removes and returns the ticket behind a secret hash, or null when
    /// none exists. A second redemption of the same secret finds nothing.
    /// </summary>
    /// <param name="tokenHash">Hash of the presented secret.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<SurfaceHandoffTicket?> ConsumeAsync(string tokenHash, CancellationToken cancellationToken = default);

    /// <summary>Removes tickets that expired before the given instant.</summary>
    /// <param name="nowUtc">Cut-off instant.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of tickets removed.</returns>
    Task<int> PurgeExpiredAsync(DateTimeOffset nowUtc, CancellationToken cancellationToken = default);
}
