using Callora.Core.Application.Surfaces;
using Callora.Surface.Rendering;
using Callora.Surface.Rendering.Rendering;
using Xunit;

namespace Callora.Core.Tests.Surface;

/// <summary>
/// Ohne zugewiesenes Theme trägt die Seite keinen Token-Block.
/// </summary>
/// <remarks>
/// Vorher stand dort ein leeres <c>&lt;style&gt;:root { }&lt;/style&gt;</c> — ein Element, das
/// aussieht, als käme das Theme gleich, und nie eines trägt. Wer nach der Ursache einer
/// Gestaltung sucht, findet es und hält es für die Stelle; die Werte kommen dann aber
/// vollständig aus den Fallbacks in <c>base.css</c> und <c>surface.css</c>.
///
/// <para>
/// Sichtbar leer ist schlimmer als nicht vorhanden: Das Fehlen des Blocks IST die Auskunft, dass
/// keiner Fläche ein Theme zugewiesen ist.
/// </para>
/// </remarks>
[Collection(SurfaceRenderingCollection.Name)]
public sealed class AnEmptyTokenBlockIsNotRenderedTests
{
    [Fact]
    public void WithoutTokensThereIsNoStyleBlock()
    {
        var html = Render(tokens: new Dictionary<string, string>(StringComparer.Ordinal));

        Assert.DoesNotContain(":root {", html, StringComparison.Ordinal);
    }

    [Fact]
    public void WithTokensTheBlockCarriesThem()
    {
        // Gegenprobe: Die Bedingung darf den Block nicht auch dann verschlucken, wenn es etwas
        // zu sagen gibt.
        var html = Render(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["color.brand"] = "#e4002b",
        });

        Assert.Contains("--cal-color-brand: #e4002b;", html, StringComparison.Ordinal);
    }

    private static string Render(IReadOnlyDictionary<string, string> tokens)
    {
        return new NunjucksSurfaceRenderer().Render(
            """{% extends "@callora/layout/page.njk" %}""",
            new SurfaceRenderContext("tenant", "workspace-a", "portal", "spa", "de", tokens),
            []);
    }
}
