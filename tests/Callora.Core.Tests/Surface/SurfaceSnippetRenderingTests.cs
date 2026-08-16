using Callora.Surface.Rendering;
using Callora.Surface.Rendering.Rendering;
using Xunit;

namespace Callora.Core.Tests.Surface;

/// <summary>
/// <c>callora_t</c> reiht sich in <c>callora_slot</c>, <c>callora_view</c> und
/// <c>callora_navigation</c> ein: Es liest nur, was der Host bereits aufgelöst hat (ADR-024 §5).
/// Ein Template stellt keine Frage an die Datenbank — es ruft die Funktion je Beschriftung einmal,
/// und jede wäre eine Abfrage.
/// </summary>
[Collection(SurfaceRenderingCollection.Name)]
public sealed class SurfaceSnippetRenderingTests
{
    [Fact]
    public void AKnownKeyRendersItsText()
    {
        var html = Render("<h1>{{ callora_t('shop.cart.title') }}</h1>", ("shop.cart.title", "Bestellung"));

        Assert.Equal("<h1>Bestellung</h1>", html);
    }

    // Der Grund, warum eine Vorlage sich schrittweise umstellen lässt: Fehlt der Schlüssel, steht
    // der eingebaute Text da — keine Zwischenstufe zeigt Schlüssel statt Text.
    [Fact]
    public void AMissingKeyFallsBackToTheTextTheTemplateBroughtAlong()
    {
        var html = Render("<h1>{{ callora_t('shop.cart.title', 'Warenkorb') }}</h1>");

        Assert.Equal("<h1>Warenkorb</h1>", html);
    }

    // Sichtbar falsch ist besser als unsichtbar leer: Wer keinen Vorgabewert mitgibt, sieht den
    // Schlüssel und weiß sofort, wo er nachzutragen ist.
    [Fact]
    public void AMissingKeyWithoutAFallbackShowsTheKey()
    {
        var html = Render("<h1>{{ callora_t('shop.cart.title') }}</h1>");

        Assert.Equal("<h1>shop.cart.title</h1>", html);
    }

    // Ein Text ist Text, kein Markup: Was ein Betreiber im Admin tippt, darf keine Skripte in die
    // Seite eines Kunden tragen.
    [Fact]
    public void ASnippetIsEscapedLikeAnyOtherValue()
    {
        var html = Render(
            "<h1>{{ callora_t('shop.cart.title') }}</h1>",
            ("shop.cart.title", "<script>alert(1)</script>"));

        Assert.DoesNotContain("<script>", html, StringComparison.Ordinal);
        Assert.Contains("&lt;script&gt;", html, StringComparison.Ordinal);
    }

    [Fact]
    public void ATemplateCanAskWhetherASnippetExists()
    {
        var html = Render(
            "{% if callora_has_snippet('shop.cart.title') %}ja{% else %}nein{% endif %}",
            ("shop.cart.title", "Bestellung"));

        Assert.Equal("ja", html);
        Assert.Equal("nein", Render("{% if callora_has_snippet('fehlt') %}ja{% else %}nein{% endif %}"));
    }

    // Ein leerer Vorgabewert ist eine Aussage („hier steht nichts") und kein fehlender Parameter.
    [Fact]
    public void AnEmptyFallbackStaysEmpty()
    {
        Assert.Equal("<p></p>", Render("<p>{{ callora_t('fehlt', '') }}</p>"));
    }

    private static string Render(string template, params (string Key, string Value)[] snippets)
    {
        var context = new SurfaceRenderContext(
            "tenant-a",
            "workspace-a",
            "portal",
            "spa",
            "de",
            new Dictionary<string, string>(StringComparer.Ordinal))
        {
            Snippets = snippets.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal),
        };

        return new NunjucksSurfaceRenderer().Render(template, context);
    }
}
