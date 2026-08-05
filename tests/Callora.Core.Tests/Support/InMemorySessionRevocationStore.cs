using Callora.Core.Application.Security;
using System.Collections.Concurrent;

namespace Callora.Core.Tests.Support;

/// <summary>Revocation list without persistence, for session-validation tests.</summary>
public sealed class InMemorySessionRevocationStore : IBackendSessionRevocationStore
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> _revoked = new(StringComparer.Ordinal);

    public Task RevokeAsync(string tokenId, DateTimeOffset expiresAtUtc, CancellationToken cancellationToken = default)
    {
        _revoked[tokenId.Trim()] = expiresAtUtc;
        return Task.CompletedTask;
    }

    public Task<bool> IsRevokedAsync(string tokenId, CancellationToken cancellationToken = default) =>
        Task.FromResult(
            !string.IsNullOrWhiteSpace(tokenId) &&
            _revoked.TryGetValue(tokenId.Trim(), out var expiresAtUtc) &&
            expiresAtUtc > DateTimeOffset.UtcNow);

    public Task<int> PurgeExpiredAsync(CancellationToken cancellationToken = default)
    {
        var nowUtc = DateTimeOffset.UtcNow;
        var expired = _revoked.Where(x => x.Value <= nowUtc).Select(x => x.Key).ToArray();
        foreach (var tokenId in expired)
        {
            _revoked.TryRemove(tokenId, out _);
        }

        return Task.FromResult(expired.Length);
    }
}
