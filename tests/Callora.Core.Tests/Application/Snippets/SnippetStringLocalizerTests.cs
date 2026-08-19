using Callora.Core.Application.Snippets;

namespace Callora.Core.Tests.Application.Snippets;

/// <summary>
/// Plugins konsumieren über <c>IStringLocalizer</c>, den .NET-Standardvertrag (ADR-024 §2 Punkt 7).
/// Die Auflösungskette bleibt vollständig dahinter — ein Plugin sieht nur den Standard.
/// </summary>
public sealed class SnippetStringLocalizerTests
{
    [Fact]
    public void AKnownKey_ReturnsItsText()
    {
        var localizer = new SnippetStringLocalizer(() => Catalog(("composer.editor.save", "Speichern")));

        var localized = localizer["composer.editor.save"];

        Assert.Equal("Speichern", localized.Value);
        Assert.False(localized.ResourceNotFound);
    }

    // Der Vertrag verlangt den Namen als Rückfall und die Meldung, dass nichts gefunden wurde.
    // Genau darauf setzt die schrittweise Migration der Oberflächen auf: Der Aufrufer zeigt dann
    // seinen eingebauten Text statt eines Schlüssels.
    [Fact]
    public void AnUnknownKey_ReturnsTheNameAndSaysSo()
    {
        var localizer = new SnippetStringLocalizer(() => Catalog());

        var localized = localizer["gibt.es.nicht"];

        Assert.Equal("gibt.es.nicht", localized.Value);
        Assert.True(localized.ResourceNotFound);
    }

    [Fact]
    public void AKeyWithArguments_IsFormatted()
    {
        var localizer = new SnippetStringLocalizer(() => Catalog(("cart.count", "{0} Artikel im Warenkorb")));

        Assert.Equal("3 Artikel im Warenkorb", localizer["cart.count", 3].Value);
    }

    // Ein leerer Katalog ist der Normalfall, solange nichts geladen wurde — eine Oberfläche zeigt
    // dann ihren eingebauten Text und keine Schlüssel.
    [Fact]
    public void AnEmptyCatalog_IsNotAnError()
    {
        var localizer = new SnippetStringLocalizer(() => new SnippetCatalog(new EmptyResolver()));

        Assert.True(localizer["irgendwas"].ResourceNotFound);
        Assert.Empty(localizer.GetAllStrings(includeParentCultures: true));
    }

    [Fact]
    public async Task TheCatalog_HoldsWhatItLoadedForThisRequest()
    {
        var catalog = new SnippetCatalog(new StaticResolver([("cart.title", "Bestellung")]));

        await catalog.LoadAsync("de-DE", workspaceKey: "acme");

        Assert.Equal("de-DE", catalog.Locale);
        Assert.Equal("Bestellung", catalog.Snippets["cart.title"]);
    }

    private static ISnippetCatalog Catalog(params (string Key, string Value)[] snippets)
    {
        var catalog = new SnippetCatalog(new StaticResolver(snippets));
        catalog.LoadAsync("de").GetAwaiter().GetResult();
        return catalog;
    }

    private sealed class StaticResolver((string Key, string Value)[] snippets) : ISnippetResolver
    {
        public Task<IReadOnlyDictionary<string, string>> ResolveAsync(
            string? locale,
            string? tenantKey = null,
            string? workspaceKey = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyDictionary<string, string>>(
                snippets.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal));
    }

    private sealed class EmptyResolver : ISnippetResolver
    {
        public Task<IReadOnlyDictionary<string, string>> ResolveAsync(
            string? locale,
            string? tenantKey = null,
            string? workspaceKey = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyDictionary<string, string>>(
                new Dictionary<string, string>(StringComparer.Ordinal));
    }
}
