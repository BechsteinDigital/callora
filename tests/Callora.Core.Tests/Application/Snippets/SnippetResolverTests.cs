using Callora.Core.Application.Configuration;
using Callora.Core.Application.Snippets;
using Callora.Core.Domain.Snippets;

namespace Callora.Core.Tests.Application.Snippets;

/// <summary>
/// Die Entscheidung aus ADR-024: Ein Wert wird über (Schlüssel, Locale, Geltungsbereich) adressiert,
/// und es gibt ZWEI Ketten — der Geltungsbereich wird zuerst durchlaufen, die Locale erst innerhalb.
/// </summary>
public sealed class SnippetResolverTests
{
    private const string Key = "cart.title";

    // Das Beispiel aus der ADR, Wort für Wort: Ein Override im Workspace auf `de` schlägt die
    // Paketdatei auf `de-DE`. Andersherum käme „Warenkorb" heraus, und ein Betreiber, der einmal
    // „Bestellung" tippt, müsste das für de, de-DE, de-AT und de-CH einzeln tun.
    [Fact]
    public async Task WorkspaceOverrideOnTheLanguage_BeatsThePackageFileOnTheRegion()
    {
        var resolver = Resolver(
            packageFile: new() { ["de-DE"] = new() { [Key] = "Warenkorb" } },
            overrides: [Override(Key, "de", SystemConfigScopes.Workspace, "acme", "Bestellung")]);

        var snippets = await resolver.ResolveAsync("de-DE", workspaceKey: "acme");

        Assert.Equal("Bestellung", snippets[Key]);
    }

    // Ein Override ist eine Absicht, eine Regionalvariante nur eine Verfeinerung — innerhalb
    // desselben Geltungsbereichs gewinnt deshalb die genauere Locale.
    [Fact]
    public async Task WithinOneScope_TheMoreSpecificLocaleWins()
    {
        var resolver = Resolver(
            packageFile: [],
            overrides:
            [
                Override(Key, "de", SystemConfigScopes.Workspace, "acme", "Bestellung"),
                Override(Key, "de-DE", SystemConfigScopes.Workspace, "acme", "Bestellung (DE)"),
            ]);

        var snippets = await resolver.ResolveAsync("de-DE", workspaceKey: "acme");

        Assert.Equal("Bestellung (DE)", snippets[Key]);
    }

    [Fact]
    public async Task AWorkspaceOverride_BeatsTenantAndGlobal()
    {
        var resolver = Resolver(
            packageFile: new() { ["de"] = new() { [Key] = "Warenkorb" } },
            overrides:
            [
                Override(Key, "de", SystemConfigScopes.Global, string.Empty, "Korb"),
                Override(Key, "de", SystemConfigScopes.Tenant, "kunde-a", "Sammlung"),
                Override(Key, "de", SystemConfigScopes.Workspace, "acme", "Bestellung"),
            ]);

        var snippets = await resolver.ResolveAsync("de", tenantKey: "kunde-a", workspaceKey: "acme");

        Assert.Equal("Bestellung", snippets[Key]);
    }

    // Ohne Workspace endet die Kette früher — der Admin läuft ohne einen, und dort darf kein
    // Workspace-Filter an einer Stelle greifen, an der es keinen Workspace gibt.
    [Fact]
    public async Task WithoutAWorkspace_TheChainEndsAtTheTenant()
    {
        var resolver = Resolver(
            packageFile: [],
            overrides:
            [
                Override(Key, "de", SystemConfigScopes.Tenant, "kunde-a", "Sammlung"),
                Override(Key, "de", SystemConfigScopes.Workspace, "acme", "Bestellung"),
            ]);

        var snippets = await resolver.ResolveAsync("de", tenantKey: "kunde-a");

        Assert.Equal("Sammlung", snippets[Key]);
    }

    [Fact]
    public async Task WithoutAnyOverride_ThePackageFileIsTheAnswer()
    {
        var resolver = Resolver(
            packageFile: new() { ["de"] = new() { [Key] = "Warenkorb" } },
            overrides: []);

        Assert.Equal("Warenkorb", (await resolver.ResolveAsync("de-DE"))[Key]);
    }

    // Die Datenbank enthält ausschließlich Abweichungen: Ein gelöschter Override führt zurück zur
    // Basis, ohne dass jemand die Basis kennen muss.
    [Fact]
    public async Task RemovingTheOverride_FallsBackToTheBaseWithoutCopyingAnything()
    {
        var resolver = Resolver(
            packageFile: new() { ["de"] = new() { [Key] = "Warenkorb" } },
            overrides: []);

        Assert.Equal("Warenkorb", (await resolver.ResolveAsync("de", workspaceKey: "acme"))[Key]);
    }

    // Ein fehlender Schlüssel darf keine leere Seite ergeben — er fehlt einfach im Wörterbuch, und
    // der Aufrufer entscheidet, was er stattdessen zeigt.
    [Fact]
    public async Task AnUnknownKey_IsSimplyAbsent()
    {
        var resolver = Resolver(packageFile: [], overrides: []);

        Assert.False((await resolver.ResolveAsync("de")).ContainsKey("gibt.es.nicht"));
    }

    // Workspace-Schlüssel werden nirgends kleingeschrieben; ein Vergleich, der die Schreibweise
    // ignoriert, macht aus zwei getrennten Workspaces einen (dieselbe Begründung wie im
    // SystemConfigResolver).
    [Fact]
    public async Task ScopeKeys_AreComparedOrdinally()
    {
        var resolver = Resolver(
            packageFile: new() { ["de"] = new() { [Key] = "Warenkorb" } },
            overrides: [Override(Key, "de", SystemConfigScopes.Workspace, "ACME", "Bestellung")]);

        var snippets = await resolver.ResolveAsync("de", workspaceKey: "acme");

        Assert.Equal("Warenkorb", snippets[Key]);
    }

    private static SnippetResolver Resolver(
        Dictionary<string, Dictionary<string, string>> packageFile,
        IReadOnlyList<SnippetOverride> overrides)
        => new(new StaticSnippetBaseSource(packageFile), new StaticSnippetOverrideStore(overrides));

    private static SnippetOverride Override(
        string key,
        string locale,
        string scope,
        string scopeKey,
        string value)
        => SnippetOverride.Create(key, locale, scope, scopeKey, value, "tester", DateTimeOffset.UtcNow);

    private sealed class StaticSnippetBaseSource(Dictionary<string, Dictionary<string, string>> byLocale)
        : ISnippetBaseSource
    {
        public ValueTask<IReadOnlyDictionary<string, string>> GetAsync(
            string locale,
            CancellationToken cancellationToken = default)
            => new(byLocale.TryGetValue(locale, out var snippets)
                ? snippets
                : new Dictionary<string, string>());
    }

    private sealed class StaticSnippetOverrideStore(IReadOnlyList<SnippetOverride> overrides)
        : ISnippetOverrideStore
    {
        public Task<IReadOnlyList<SnippetOverride>> ListAsync(
            IReadOnlyList<(string Scope, string ScopeKey)> scopeChain,
            IReadOnlyList<string> locales,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<SnippetOverride>>(
                [.. overrides.Where(entry =>
                    scopeChain.Any(scope =>
                        scope.Scope == entry.Scope &&
                        string.Equals(scope.ScopeKey, entry.ScopeKey, StringComparison.Ordinal)) &&
                    locales.Contains(entry.Locale, StringComparer.OrdinalIgnoreCase))]);


        public Task<IReadOnlyList<SnippetOverride>> ListForScopeAsync(
            string scope,
            string scopeKey,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<SnippetOverride>>([]);

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
