using Callora.Core.Application.Configuration;
using Callora.Core.Application.Snippets;
using Callora.Core.Domain.Snippets;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace Callora.Core.Tests.Application.Snippets;

/// <summary>
/// Snippets werden pro Anfrage in großer Zahl gelesen; ohne Cache wäre das der nächste
/// Hot-Path-Befund (ADR-024 §4). Gezählt wird über den Verbrauch, nicht über die Zeit: Wie schnell
/// etwas ist, hängt an der Maschine, wie oft es die Datenbank fragt, nicht.
/// </summary>
public sealed class CachedSnippetResolverTests
{
    [Fact]
    public async Task ASecondResolutionDoesNotTouchTheStoreAgain()
    {
        var (resolver, store) = Create();

        var first = await resolver.ResolveAsync("de", workspaceKey: "acme");
        var second = await resolver.ResolveAsync("de", workspaceKey: "acme");

        Assert.Equal("Bestellung", first["cart.title"]);
        Assert.Equal("Bestellung", second["cart.title"]);
        Assert.Equal(1, store.Calls);
    }

    // Verschiedene Ketten sind verschiedene Antworten: Ein Eintrag für einen Workspace darf nicht
    // für einen anderen gelten, und ein Cache, der das verwechselt, zeigt fremde Texte.
    [Fact]
    public async Task DifferentScopesAreDifferentEntries()
    {
        var (resolver, store) = Create();

        await resolver.ResolveAsync("de", workspaceKey: "acme");
        await resolver.ResolveAsync("de", workspaceKey: "andere");
        await resolver.ResolveAsync("en", workspaceKey: "acme");

        Assert.Equal(3, store.Calls);
    }

    [Fact]
    public async Task InvalidatingOneWorkspaceLeavesTheOthersAlone()
    {
        var (resolver, store) = Create();
        await resolver.ResolveAsync("de", workspaceKey: "acme");
        await resolver.ResolveAsync("de", workspaceKey: "andere");

        ((ISnippetCache)resolver).InvalidateWorkspace("acme");
        await resolver.ResolveAsync("de", workspaceKey: "acme");
        await resolver.ResolveAsync("de", workspaceKey: "andere");

        Assert.Equal(3, store.Calls);
    }

    // Die Kette eines Workspaces enthält die Mandantenebene — ihr Ergebnis hängt also an ihr.
    [Fact]
    public async Task InvalidatingATenantAlsoDropsItsWorkspaces()
    {
        var (resolver, store) = Create();
        await resolver.ResolveAsync("de", tenantKey: "kunde-a", workspaceKey: "acme");

        ((ISnippetCache)resolver).InvalidateTenant("kunde-a");
        await resolver.ResolveAsync("de", tenantKey: "kunde-a", workspaceKey: "acme");

        Assert.Equal(2, store.Calls);
    }

    // Eine geänderte Basis — ein installiertes, aktualisiertes oder entferntes Paket — liegt unter
    // jeder Kette.
    [Fact]
    public async Task InvalidatingEverythingDropsEveryChain()
    {
        var (resolver, store) = Create();
        await resolver.ResolveAsync("de", workspaceKey: "acme");
        await resolver.ResolveAsync("de", workspaceKey: "andere");

        ((ISnippetCache)resolver).InvalidateAll();
        await resolver.ResolveAsync("de", workspaceKey: "acme");
        await resolver.ResolveAsync("de", workspaceKey: "andere");

        Assert.Equal(4, store.Calls);
    }

    private static (CachedSnippetResolver Resolver, CountingSnippetOverrideStore Store) Create()
    {
        var store = new CountingSnippetOverrideStore();
        var services = new ServiceCollection();
        services.AddSingleton<ISnippetBaseSource>(new EmptySnippetBaseSource());
        services.AddSingleton<ISnippetOverrideStore>(store);
        services.AddScoped<SnippetResolver>();
        var provider = services.BuildServiceProvider();

        return (
            new CachedSnippetResolver(new MemoryCache(new MemoryCacheOptions()), provider.GetRequiredService<IServiceScopeFactory>()),
            store);
    }

    private sealed class EmptySnippetBaseSource : ISnippetBaseSource
    {
        public ValueTask<IReadOnlyDictionary<string, string>> GetAsync(
            string locale,
            CancellationToken cancellationToken = default)
            => new(new Dictionary<string, string>(StringComparer.Ordinal));
    }

    private sealed class CountingSnippetOverrideStore : ISnippetOverrideStore
    {
        public int Calls { get; private set; }

        public Task<IReadOnlyList<SnippetOverride>> ListAsync(
            IReadOnlyList<(string Scope, string ScopeKey)> scopeChain,
            IReadOnlyList<string> locales,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult<IReadOnlyList<SnippetOverride>>(
            [
                SnippetOverride.Create(
                    "cart.title",
                    locales[0],
                    scopeChain[^1].Scope,
                    scopeChain[^1].ScopeKey,
                    "Bestellung",
                    "tester",
                    DateTimeOffset.UtcNow),
            ]);
        }

        public Task UpsertAsync(SnippetOverride entry, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task RemoveAsync(
            string snippetKey,
            string locale,
            string scope,
            string scopeKey,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
