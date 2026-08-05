using System.Collections.Concurrent;
using Callora.Core.Application.Surfaces;

namespace Callora.Core.Tests.Support;

/// <summary>
/// In-memory <see cref="ISurfaceHandoffTicketStore"/>. Consumption removes the entry
/// with <c>TryRemove</c>, so single use holds under concurrency here just as the
/// delete-and-return does in the relational store.
/// </summary>
public sealed class InMemorySurfaceHandoffTicketStore : ISurfaceHandoffTicketStore
{
    private readonly ConcurrentDictionary<string, SurfaceHandoffTicket> _tickets = new(StringComparer.Ordinal);

    /// <summary>Tickets currently stored.</summary>
    public IReadOnlyCollection<SurfaceHandoffTicket> Tickets => _tickets.Values.ToArray();

    public Task CreateAsync(
        SurfaceHandoffTicket ticket,
        string tokenHash,
        CancellationToken cancellationToken = default)
    {
        _tickets[tokenHash] = ticket;
        return Task.CompletedTask;
    }

    public Task<SurfaceHandoffTicket?> ConsumeAsync(
        string tokenHash,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(_tickets.TryRemove(tokenHash, out var ticket) ? ticket : null);

    public Task<int> PurgeExpiredAsync(DateTimeOffset nowUtc, CancellationToken cancellationToken = default)
    {
        var doomed = _tickets.Where(x => x.Value.ExpiresAtUtc <= nowUtc).Select(x => x.Key).ToList();
        foreach (var key in doomed)
        {
            _tickets.TryRemove(key, out _);
        }

        return Task.FromResult(doomed.Count);
    }
}
