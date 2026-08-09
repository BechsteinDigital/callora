using System.Text.RegularExpressions;
using Callora.Core.Application.Extensions;
using Callora.Core.Application.Surfaces.Layout;
using Callora.Surface.Rendering.Rendering.Composition;
using Xunit;

namespace Callora.Core.Tests.Surface;

/// <summary>
/// Was der Kompositions-Renderer ausgibt, muss ein Stylesheet erreichen, das die ausgelieferte
/// Seite auch verlinkt.
/// </summary>
/// <remarks>
/// <see cref="SurfaceBaseSectionLayoutsTests"/> prüft, dass jedes angebotene Layout in
/// <c>tokens.scss</c> eine Rasterregel hat. Das genügte nicht: Vite baut daraus
/// <c>surface-app/surface.css</c>, und diese Datei war lange da, ohne dass eine Seite sie
/// verlinkte — im IIFE-Modus injiziert Vite sie nicht selbst.
///
/// <para>
/// Die Folge war in jeder Suite unsichtbar. Der Server lieferte korrekte Sektionen aus, der
/// Browser bekam Klassen, auf die nichts hörte, und im Composer sah dieselbe Seite richtig aus,
/// weil der Editor die Tokens direkt einbettet. Zwei Stellen entschieden dasselbe, ohne
/// voneinander zu wissen — die Quelle war geprüft, die Auslieferung nicht.
/// </para>
///
/// <para>
/// Deshalb geht dieser Test vom <em>ausgelieferten</em> Ende aus: von den <c>&lt;link&gt;</c>-Zeilen
/// in <c>base.njk</c> und den Dateien, auf die sie zeigen.
/// </para>
/// </remarks>
public sealed class ComposedSectionsReachTheirStylesheetTests
{
    [Fact]
    public void TheStylesheetsTheBaseTemplateLinksExist()
    {
        // Ein <link> auf eine Datei, die der Build nicht erzeugt, ist ein 404 im <head> —
        // sichtbar nur in der Netzwerkkonsole, folgenlos für jeden Statuscode der Seite.
        var linked = LinkedStylesheets();

        Assert.NotEmpty(linked);
        foreach (var (href, _) in linked)
        {
            Assert.True(File.Exists(PathOf(href)), $"{href} ist verlinkt, existiert aber nicht.");
        }
    }

    [Fact]
    public void EveryClassTheRendererEmitsIsStyledByALinkedStylesheet()
    {
        var css = DeliveredCss();

        foreach (var name in ClassesIn(RenderOneSection()))
        {
            Assert.True(
                css.Contains("." + name, StringComparison.Ordinal),
                $"Der Renderer gibt „{name}\" aus, aber kein ausgeliefertes Stylesheet kennt die Klasse.");
        }
    }

    [Fact]
    public void EveryBaseSectionLayoutHasItsGridRuleInADeliveredStylesheet()
    {
        // Die Basis-Layouts sind das, was eine frische Installation anbietet. Eines davon ohne
        // Rasterregel auszuliefern hieße: Der Anwender wählt „Zwei Spalten", bekommt eine, und
        // nirgends steht ein Fehler.
        var css = WithoutQuotes(DeliveredCss());

        foreach (var layout in SurfaceBaseSectionLayouts.All)
        {
            Assert.True(
                css.Contains($"[data-cal-layout={layout.LayoutKey}]", StringComparison.Ordinal),
                $"""
                 Das Basis-Layout „{layout.LayoutKey}" hat keine Regel in einem ausgelieferten Stylesheet.
                 Verlinkt sind: {string.Join(", ", LinkedStylesheets().Select(sheet => sheet.Href))}.
                 """);
        }
    }

    private static string RenderOneSection() =>
        new SurfaceCompositionRenderer().Render(
            new SurfaceLayoutDocument(
                Key: "start",
                VersionNumber: 1,
                Sections:
                [
                    new SurfaceLayoutSection(
                        Layout: "two-1-1",
                        Position: 0,
                        Blocks: [new SurfaceLayoutBlock("demo.block", "main", 0, new Dictionary<string, SurfaceBlockBinding>())]),
                ]));

    private static IEnumerable<string> ClassesIn(string markup) =>
        Regex
            .Matches(markup, "class=\"([^\"]+)\"")
            .SelectMany(match => match.Groups[1].Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            // Inseln hydriert das JS-Bundle; ihre Gestaltung bringt die Vue-Komponente mit.
            .Where(name => !name.StartsWith("callora-island", StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal);

    private static string DeliveredCss() =>
        string.Concat(LinkedStylesheets().Select(sheet => sheet.Text));

    /// <summary>Minifiziertes CSS schreibt <c>[a=b]</c>, die Quelle <c>[a='b']</c>.</summary>
    private static string WithoutQuotes(string css) =>
        css.Replace("'", string.Empty, StringComparison.Ordinal)
            .Replace("\"", string.Empty, StringComparison.Ordinal);

    private static IReadOnlyList<(string Href, string Text)> LinkedStylesheets()
    {
        var template = File.ReadAllText(
            Path.Combine(RepositoryRoot(), "src", "Surface.Rendering", "Resources", "views", "surface", "base.njk"));

        return Regex
            .Matches(template, "<link rel=\"stylesheet\" href=\"([^\"]+)\"")
            .Select(match => match.Groups[1].Value)
            .Select(href => (href, File.Exists(PathOf(href)) ? File.ReadAllText(PathOf(href)) : string.Empty))
            .ToList();
    }

    private static string PathOf(string href) =>
        Path.Combine(
            RepositoryRoot(),
            "src",
            "Surface.Rendering",
            "wwwroot",
            href.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Callora.Host.sln")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory!.FullName;
    }
}
