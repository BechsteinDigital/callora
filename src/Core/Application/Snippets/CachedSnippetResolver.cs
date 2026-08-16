using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;

namespace Callora.Core.Application.Snippets;

/// <summary>
/// Hält aufgelöste Wörterbücher je (Geltungsbereichs-Kette, Locale) vor (ADR-024 §4).
/// </summary>
/// <remarks>
/// Snippets werden pro Anfrage in großer Zahl gelesen. Ohne Cache wäre das der nächste
/// Hot-Path-Befund, wie ihn #268 für die Flächenroute und #280 für das Theme hatte — dieselbe
/// Mechanik wie dort, und aus demselben Grund: Der Renderpfad zieht einen Eintrag statt N
/// Abfragen.
///
/// <para>
/// Invalidiert wird in derselben Granularität, in der geschrieben wird. Ein Override im Workspace
/// trifft genau dessen Einträge, einer im Mandanten dessen Kette, einer im globalen Bereich alles.
/// Und eine geänderte Basis — ein installiertes, aktualisiertes oder entferntes Plugin — trifft
/// ebenfalls alles: Sie liegt unter jeder Kette.
/// </para>
///
/// <para>
/// Der Index neben dem Cache ist nötig, weil <see cref="IMemoryCache"/> sich nicht aufzählen
/// lässt. Er hält nur, was zum gezielten Entfernen reicht — Locale und Kette je Schlüssel.
/// </para>
/// </remarks>
public sealed class CachedSnippetResolver(IMemoryCache cache, IServiceScopeFactory scopeFactory)
    : ISnippetResolver, ISnippetCache
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(2);

    private readonly ConcurrentDictionary<string, (string TenantKey, string WorkspaceKey)> _entries =
        new(StringComparer.Ordinal);

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, string>> ResolveAsync(
        string? locale,
        string? tenantKey = null,
        string? workspaceKey = null,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = BuildCacheKey(locale, tenantKey, workspaceKey);
        return cache.TryGetValue(cacheKey, out IReadOnlyDictionary<string, string>? cached) && cached is not null
            ? Task.FromResult(cached)
            : ResolveAndCacheAsync(cacheKey, locale, tenantKey, workspaceKey, cancellationToken);
    }

    /// <inheritdoc />
    public void InvalidateAll()
    {
        foreach (var cacheKey in _entries.Keys)
        {
            cache.Remove(cacheKey);
        }

        _entries.Clear();
    }

    /// <inheritdoc />
    public void InvalidateTenant(string tenantKey)
    {
        if (string.IsNullOrWhiteSpace(tenantKey))
        {
            return;
        }

        // Auch die Workspaces dieses Mandanten: Ihre Kette enthält die Mandantenebene, ihr
        // Ergebnis hängt also an ihr.
        Remove(entry => string.Equals(entry.TenantKey, tenantKey.Trim(), StringComparison.Ordinal));
    }

    /// <inheritdoc />
    public void InvalidateWorkspace(string workspaceKey)
    {
        if (string.IsNullOrWhiteSpace(workspaceKey))
        {
            return;
        }

        Remove(entry => string.Equals(entry.WorkspaceKey, workspaceKey.Trim(), StringComparison.Ordinal));
    }

    private void Remove(Func<(string TenantKey, string WorkspaceKey), bool> matches)
    {
        foreach (var pair in _entries)
        {
            if (!matches(pair.Value))
            {
                continue;
            }

            cache.Remove(pair.Key);
            _entries.TryRemove(pair.Key, out _);
        }
    }

    private async Task<IReadOnlyDictionary<string, string>> ResolveAndCacheAsync(
        string cacheKey,
        string? locale,
        string? tenantKey,
        string? workspaceKey,
        CancellationToken cancellationToken)
    {
        // Eigener Scope, weil der innere Resolver am DbContext hängt und dieser Dekorator als
        // Singleton lebt — sonst wäre er eine Captive Dependency.
        using var scope = scopeFactory.CreateScope();
        var resolved = await scope.ServiceProvider
            .GetRequiredService<SnippetResolver>()
            .ResolveAsync(locale, tenantKey, workspaceKey, cancellationToken)
            .ConfigureAwait(false);

        cache.Set(cacheKey, resolved, CacheDuration);
        _entries[cacheKey] = (tenantKey?.Trim() ?? string.Empty, workspaceKey?.Trim() ?? string.Empty);
        return resolved;
    }

    private static string BuildCacheKey(string? locale, string? tenantKey, string? workspaceKey)
        => $"snippets:{locale?.Trim()}|{tenantKey?.Trim()}|{workspaceKey?.Trim()}";
}
