using System.Collections.Concurrent;
using Callora.Core.Application.Plugins;
using Callora.Core.Domain.Plugins;

namespace Callora.Core.Tests.Support;

/// <summary>
/// In-memory <see cref="ISessionResumeTicketStore"/> for tests about the service on top of it.
/// Whether delete-and-return actually races correctly is a database property and is covered by the
/// Postgres integration test, not here.
/// </summary>
public sealed class InMemorySessionResumeTicketStore : ISessionResumeTicketStore
{
    private readonly ConcurrentDictionary<string, SessionResumeTicketRecord> _byTokenHash =
        new(StringComparer.Ordinal);

    /// <summary>Rows currently held, for assertions about what a call left behind.</summary>
    public IReadOnlyCollection<SessionResumeTicketRecord> Records => [.. _byTokenHash.Values];

    public Task CreateAsync(SessionResumeTicketRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        _byTokenHash[record.TokenHash] = record;
        return Task.CompletedTask;
    }

    public Task<SessionResumeTicketRecord?> ConsumeAsync(
        string tokenHash,
        string pluginId,
        CancellationToken cancellationToken = default)
    {
        if (!_byTokenHash.TryGetValue(tokenHash, out var record) ||
            !string.Equals(record.PluginId, pluginId, StringComparison.Ordinal))
        {
            return Task.FromResult<SessionResumeTicketRecord?>(null);
        }

        return Task.FromResult(_byTokenHash.TryRemove(tokenHash, out var removed) ? removed : null);
    }

    public Task<bool> DeleteAsync(
        string tokenHash,
        string pluginId,
        CancellationToken cancellationToken = default)
    {
        if (!_byTokenHash.TryGetValue(tokenHash, out var record) ||
            !string.Equals(record.PluginId, pluginId, StringComparison.Ordinal))
        {
            return Task.FromResult(false);
        }

        return Task.FromResult(_byTokenHash.TryRemove(tokenHash, out _));
    }

    public Task<int> PurgeExpiredAsync(DateTimeOffset nowUtc, CancellationToken cancellationToken = default)
    {
        var expired = _byTokenHash
            .Where(pair => pair.Value.ExpiresAtUtc <= nowUtc)
            .Select(pair => pair.Key)
            .ToArray();

        foreach (var key in expired)
        {
            _byTokenHash.TryRemove(key, out _);
        }

        return Task.FromResult(expired.Length);
    }
}
