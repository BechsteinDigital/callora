using Callora.Core.Application.Configuration;
using Callora.Core.Application.Snippets;
using Callora.Core.Domain.Snippets;

namespace Callora.Core.Tests.Application.Snippets;

/// <summary>
/// Was ein Betreiber mit den Texten tun kann (ADR-024 §5) — und der Grund, warum es das überhaupt
/// gibt: Wer „Warenkorb" in „Bestellung" ändern will, darf dafür kein Paket neu bauen müssen.
/// </summary>
public sealed class SnippetAdminServiceTests
{
    [Fact]
    public async Task List_ShowsWhatThePackageBroughtAndWhatWasSetHere()
    {
        var service = Create(
            basis: [Base("shop", "shop.cart.title", "de", "Warenkorb")],
            overrides: [Override("shop.cart.title", "de", SystemConfigScopes.Workspace, "acme", "Bestellung")]);

        var entry = Assert.Single(await service.ListAsync("de", SystemConfigScopes.Workspace, "acme"));

        Assert.Equal("Warenkorb", entry.BaseValue);
        Assert.Equal("Bestellung", entry.OverrideValue);
        Assert.Equal("Bestellung", entry.EffectiveValue);
        Assert.True(entry.IsOverridden);
        Assert.Equal("shop", entry.PluginId);
    }

    // Gezeigt wird EINE Ebene, nicht die aufgelöste Kette: Wer im Workspace steht, muss sehen, was
    // dort gesetzt ist — sonst kann niemand sagen, was das Zurücknehmen einer Zeile bewirkt.
    [Fact]
    public async Task List_DoesNotShowAnotherScopesOverrideAsIfItWereThisOne()
    {
        var service = Create(
            basis: [Base("shop", "shop.cart.title", "de", "Warenkorb")],
            overrides: [Override("shop.cart.title", "de", SystemConfigScopes.Global, string.Empty, "Korb")]);

        var entry = Assert.Single(await service.ListAsync("de", SystemConfigScopes.Workspace, "acme"));

        Assert.Null(entry.OverrideValue);
        Assert.False(entry.IsOverridden);
    }

    // Ein Paket, das seinen Schlüssel aufgibt, macht die Arbeit des Betreibers sonst unsichtbar —
    // und unsichtbar ist der Zustand, in dem sie später niemand mehr findet (ADR-024 §7).
    [Fact]
    public async Task List_KeepsAnOrphanedOverrideVisible()
    {
        var service = Create(
            basis: [],
            overrides: [Override("shop.gone", "de", SystemConfigScopes.Global, string.Empty, "Bleibt")]);

        var entry = Assert.Single(await service.ListAsync("de", SystemConfigScopes.Global, string.Empty));

        Assert.True(entry.IsOrphaned);
        Assert.Equal("Bleibt", entry.EffectiveValue);
    }

    [Fact]
    public async Task Set_WritesTheOverrideAndDropsTheCacheOfThatScopeOnly()
    {
        var cache = new RecordingSnippetCache();
        var overrides = new InMemorySnippetOverrideStore();
        var service = new SnippetAdminService(new InMemorySnippetBaseStore([]), overrides, cache);

        await service.SetAsync("shop.cart.title", "de", SystemConfigScopes.Workspace, "acme", "Bestellung", "tester");

        Assert.Equal("Bestellung", Assert.Single(overrides.Entries).Value);
        Assert.Equal(["workspace:acme"], cache.Invalidations);
    }

    [Fact]
    public async Task Set_InTheGlobalScope_DropsEverythingBecauseItLiesUnderEveryChain()
    {
        var cache = new RecordingSnippetCache();
        var service = new SnippetAdminService(new InMemorySnippetBaseStore([]), new InMemorySnippetOverrideStore(), cache);

        await service.SetAsync("shop.cart.title", "de", SystemConfigScopes.Global, string.Empty, "Korb", "tester");

        Assert.Equal(["all"], cache.Invalidations);
    }

    // Zurücknehmen heißt zurück zur Basis — es gibt nichts wiederherzustellen, weil beim Anlegen
    // eines Geltungsbereichs nie etwas kopiert wurde.
    [Fact]
    public async Task Reset_RemovesTheOverrideSoTheBaseAppliesAgain()
    {
        var overrides = new InMemorySnippetOverrideStore();
        await overrides.UpsertAsync(Override("shop.cart.title", "de", SystemConfigScopes.Workspace, "acme", "Bestellung"));
        var service = new SnippetAdminService(
            new InMemorySnippetBaseStore([Base("shop", "shop.cart.title", "de", "Warenkorb")]),
            overrides,
            new RecordingSnippetCache());

        await service.ResetAsync("shop.cart.title", "de", SystemConfigScopes.Workspace, "acme");

        var entry = Assert.Single(await service.ListAsync("de", SystemConfigScopes.Workspace, "acme"));
        Assert.False(entry.IsOverridden);
        Assert.Equal("Warenkorb", entry.EffectiveValue);
    }

    [Fact]
    public async Task AnUnknownScope_IsRefusedRatherThanTreatedAsGlobal()
    {
        var service = Create([], []);

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.ListAsync("de", "flaeche", "portal"));
    }

    private static SnippetAdminService Create(
        IReadOnlyList<SnippetBaseEntry> basis,
        IReadOnlyList<SnippetOverride> overrides)
        => new(
            new InMemorySnippetBaseStore(basis),
            new InMemorySnippetOverrideStore(overrides),
            new RecordingSnippetCache());

    private static SnippetBaseEntry Base(string pluginId, string key, string locale, string value)
        => SnippetBaseEntry.Create(pluginId, key, locale, value, "1.0.0");

    private static SnippetOverride Override(string key, string locale, string scope, string scopeKey, string value)
        => SnippetOverride.Create(key, locale, scope, scopeKey, value, "tester", DateTimeOffset.UtcNow);

    private sealed class InMemorySnippetBaseStore(IReadOnlyList<SnippetBaseEntry> entries) : ISnippetBaseStore
    {
        public Task<IReadOnlyList<SnippetBaseEntry>> ListForLocaleAsync(
            string locale,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<SnippetBaseEntry>>(
                [.. entries.Where(entry => string.Equals(entry.Locale, locale, StringComparison.OrdinalIgnoreCase))]);

        public Task ReplaceForPluginAsync(
            string pluginId,
            IReadOnlyList<SnippetBaseEntry> replacement,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task ClearForPluginAsync(string pluginId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class InMemorySnippetOverrideStore : ISnippetOverrideStore
    {
        private readonly List<SnippetOverride> _entries;

        public InMemorySnippetOverrideStore(IReadOnlyList<SnippetOverride>? seed = null) => _entries = [.. seed ?? []];

        public IReadOnlyList<SnippetOverride> Entries => _entries;

        public Task<IReadOnlyList<SnippetOverride>> ListAsync(
            IReadOnlyList<(string Scope, string ScopeKey)> scopeChain,
            IReadOnlyList<string> locales,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<SnippetOverride>>([.. _entries]);

        public Task<IReadOnlyList<SnippetOverride>> ListForScopeAsync(
            string scope,
            string scopeKey,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<SnippetOverride>>(
                [.. _entries.Where(entry =>
                    entry.Scope == scope && string.Equals(entry.ScopeKey, scopeKey, StringComparison.Ordinal))]);

        public Task UpsertAsync(SnippetOverride entry, CancellationToken cancellationToken = default)
        {
            _entries.RemoveAll(candidate =>
                candidate.SnippetKey == entry.SnippetKey
                && candidate.Locale == entry.Locale
                && candidate.Scope == entry.Scope
                && candidate.ScopeKey == entry.ScopeKey);
            _entries.Add(entry);
            return Task.CompletedTask;
        }

        public Task RemoveAsync(
            string snippetKey,
            string locale,
            string scope,
            string scopeKey,
            CancellationToken cancellationToken = default)
        {
            _entries.RemoveAll(entry =>
                entry.SnippetKey == snippetKey
                && entry.Locale == locale
                && entry.Scope == scope
                && entry.ScopeKey == scopeKey);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingSnippetCache : ISnippetCache
    {
        private readonly List<string> _invalidations = [];

        public IReadOnlyList<string> Invalidations => _invalidations;

        public void InvalidateAll() => _invalidations.Add("all");

        public void InvalidateTenant(string tenantKey) => _invalidations.Add($"tenant:{tenantKey}");

        public void InvalidateWorkspace(string workspaceKey) => _invalidations.Add($"workspace:{workspaceKey}");
    }
}
