using Callora.Core.Extensibility;
using Microsoft.Extensions.Caching.Memory;

namespace Callora.Core.Infrastructure.Security;

/// <summary>
/// Bounded, short-lived cache of account revocation state, shared by the scoped
/// <see cref="BackendSessionValidator"/> instances (#105).
/// <para>
/// Owns its own <see cref="MemoryCache"/> rather than the ambient one: the size cap
/// is the point — an attacker sending unknown subjects must not be able to grow it —
/// and a cap on a shared cache would force every other consumer to size its entries.
/// </para>
/// </summary>
[CalloraInternal("Session-state cache — not a plugin contract (REV2 §7.2)")]
public sealed class BackendSessionStateCache : IDisposable
{
    /// <summary>How long account state is reused before it is read again.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(15);

    private const int MaxEntries = 5_000;

    private readonly MemoryCache _cache = new(new MemoryCacheOptions { SizeLimit = MaxEntries });

    /// <summary>Returns the cached state, or null when the subject is not cached.</summary>
    public bool TryGet(string subject, out BackendSessionAccountState? state) =>
        _cache.TryGetValue(subject, out state);

    /// <summary>Caches <paramref name="state"/> (including "no such account") for the window.</summary>
    public void Set(string subject, BackendSessionAccountState? state) =>
        _cache.Set(
            subject,
            state,
            new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = Lifetime,
                Size = 1
            });

    /// <summary>Drops a subject, so a revocation takes effect without waiting out the window.</summary>
    public void Invalidate(string subject) => _cache.Remove(subject);

    public void Dispose() => _cache.Dispose();
}
