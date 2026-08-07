using Callora.Core.Infrastructure.Extensions;
using System.Text.Json;
using Xunit;

namespace Callora.Core.Tests.Infrastructure.Extensions;

/// <summary>
/// Was ein Theme über seine Sektionslayouts sagen kann — und was passiert, wenn es sich vertut.
/// <para>
/// Der Parser überspringt Kaputtes, statt aufzugeben. Ein Theme mit einem fehlerhaften Layout
/// bietet seine anderen weiter an; ein Parser, der abbräche, ließe den Editor ohne jedes Layout
/// zurück — und das sieht genauso aus wie ein Theme, das keine deklariert.
/// </para>
/// </summary>
public sealed class SectionLayoutManifestReaderTests
{
    private static IReadOnlyList<Callora.Core.Application.Extensions.SectionLayoutDefinition> Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        return SectionLayoutManifestReader.Parse(document.RootElement);
    }

    [Fact]
    public void ReadsLayoutsWithTheirRegions()
    {
        var layouts = Parse("""
        {
          "sectionLayouts": [
            {
              "key": "two-2-1",
              "label": "Zwei Spalten (2:1)",
              "regions": [
                { "key": "main", "label": "Inhalt" },
                { "key": "aside", "label": "Seitenspalte" }
              ]
            }
          ]
        }
        """);

        var layout = Assert.Single(layouts);
        Assert.Equal("two-2-1", layout.LayoutKey);
        Assert.Equal("Zwei Spalten (2:1)", layout.Label);
        Assert.Equal(["main", "aside"], layout.Regions.Select(region => region.RegionKey));
    }

    [Fact]
    public void KeepsTheDeclaredRegionOrder()
    {
        // Die Reihenfolge des Themes ist die Lesereihenfolge. Alphabetisch sortiert stünde
        // "aside" vor "main" — die Seitenspalte vor dem Inhalt, neben dem sie sitzt.
        var layouts = Parse("""
        { "sectionLayouts": [ { "key": "x", "regions": ["main", "aside", "footer"] } ] }
        """);

        Assert.Equal(["main", "aside", "footer"], layouts[0].Regions.Select(region => region.RegionKey));
    }

    [Fact]
    public void AcceptsARegionThatIsJustItsKey()
    {
        // Die Form, in der ein Theme-Autor Regionen zuerst hinschreibt.
        var layouts = Parse("""{ "sectionLayouts": [ { "key": "single", "regions": ["main"] } ] }""");

        var region = Assert.Single(layouts[0].Regions);
        Assert.Equal("main", region.RegionKey);
        Assert.Equal("main", region.Label);
    }

    [Fact]
    public void SkipsALayoutWithoutAKeyAndKeepsTheRest()
    {
        // Ohne Schlüssel gibt es nichts, worauf das Theme-CSS zielen könnte.
        var layouts = Parse("""
        {
          "sectionLayouts": [
            { "label": "Namenlos" },
            { "key": "single", "label": "Eine Spalte" }
          ]
        }
        """);

        Assert.Equal(["single"], layouts.Select(layout => layout.LayoutKey));
    }

    [Fact]
    public void FallsBackToTheKeyWhenNoLabelIsGiven()
    {
        var layouts = Parse("""{ "sectionLayouts": [ { "key": "sidebar-left" } ] }""");

        Assert.Equal("sidebar-left", layouts[0].Label);
    }

    [Fact]
    public void AcceptsTheShorterKeyName()
    {
        // `layouts` statt `sectionLayouts` — dieselbe Nachsicht, die der Rest von theme.json
        // schon zeigt.
        var layouts = Parse("""{ "layouts": [ { "key": "single" } ] }""");

        Assert.Equal(["single"], layouts.Select(layout => layout.LayoutKey));
    }

    [Fact]
    public void ReadsNoLayoutsWhenTheManifestDeclaresNone()
    {
        // Ein gültiger Zustand: Ein Theme, das nur Token setzt, hat keine Layouts anzubieten.
        Assert.Empty(Parse("""{ "settings": [] }"""));
        Assert.Empty(Parse("""{ "sectionLayouts": "kein Array" }"""));
        Assert.Empty(Parse("""[]"""));
    }

    [Fact]
    public void GivesLayoutsAnAscendingOrderWhenNoneIsDeclared()
    {
        // Sonst stünden sie alle auf 0 und die Auswahl im Editor sortierte nach etwas anderem —
        // meist nach dem Schlüssel, also alphabetisch, was die Absicht des Themes verwirft.
        var layouts = Parse("""
        { "sectionLayouts": [ { "key": "b" }, { "key": "a" }, { "key": "c" } ] }
        """);

        Assert.Equal(["b", "a", "c"], layouts.Select(layout => layout.LayoutKey));
        Assert.True(layouts[0].SortOrder < layouts[1].SortOrder);
        Assert.True(layouts[1].SortOrder < layouts[2].SortOrder);
    }

    [Fact]
    public void LetsTheThemeDeclareItsOwnOrder()
    {
        var layouts = Parse("""
        { "sectionLayouts": [ { "key": "b", "sortOrder": 5 }, { "key": "a", "order": 1 } ] }
        """);

        Assert.Equal(5, layouts[0].SortOrder);
        Assert.Equal(1, layouts[1].SortOrder);
    }
}
