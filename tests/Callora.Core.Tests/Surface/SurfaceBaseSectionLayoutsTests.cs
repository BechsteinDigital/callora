using Callora.Core.Application.Extensions;
using System.Text.RegularExpressions;
using Xunit;

namespace Callora.Core.Tests.Surface;

/// <summary>
/// Die Basis-Sektionslayouts gegen das Stylesheet, das sie darstellt.
/// <para>
/// Die Deklaration steht in C# (der Renderer braucht sie), das CSS in
/// <c>tokens.scss</c> (die Fläche lädt es). Zwei Dateien, zwei Sprachen, dieselbe Sache —
/// und nichts zwingt sie zusammen außer diesem Test. Ein deklariertes Layout ohne Regel ist
/// ein Angebot, das nichts tut: im Editor wählbar, auf der Seite wirkungslos. Eine Regel ohne
/// Deklaration ist ein Raster, das niemand wählen kann. Beides fiele sonst erst auf der
/// veröffentlichten Seite auf, und dort sieht es aus wie ein Fehler im Renderer.
/// </para>
/// </summary>
public sealed class SurfaceBaseSectionLayoutsTests
{
    private static readonly string Css = File.ReadAllText(FindTokensScss());

    /// <summary>Die Layout-Schlüssel, auf die das Stylesheet zielt.</summary>
    private static HashSet<string> StyledLayouts() =>
        Regex.Matches(Css, @"data-cal-layout='([^']+)'")
            .Select(match => match.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);

    [Fact]
    public void TheBaseOffersSectionLayouts()
    {
        // Ohne sie hätte eine frische Installation nichts, womit sich etwas komponieren ließe:
        // Der Renderer gäbe data-cal-layout aus, der Editor böte keine Wahl, und beides sähe
        // nach einem Fehler aus statt nach einem fehlenden Theme.
        Assert.NotEmpty(SurfaceBaseSectionLayouts.All);
    }

    [Fact]
    public void EveryDeclaredLayoutIsStyled()
    {
        var styled = StyledLayouts();

        var unstyled = SurfaceBaseSectionLayouts.All
            .Select(layout => layout.LayoutKey)
            .Where(key => !styled.Contains(key))
            .ToArray();

        Assert.Empty(unstyled);
    }

    [Fact]
    public void EveryStyledLayoutIsDeclared()
    {
        var declared = SurfaceBaseSectionLayouts.All
            .Select(layout => layout.LayoutKey)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Empty(StyledLayouts().Where(key => !declared.Contains(key)).ToArray());
    }

    [Fact]
    public void EveryLayoutHasRegionsWithDistinctKeys()
    {
        // Ein Layout ohne Region ist eines, in das nichts passt; zwei gleich benannte Regionen
        // wären zwei Orte, die derselbe sind — Blöcke aus beiden landeten übereinander.
        var broken = SurfaceBaseSectionLayouts.All
            .Where(layout => layout.Regions.Count == 0 ||
                             layout.Regions.Select(region => region.RegionKey).Distinct().Count() !=
                             layout.Regions.Count)
            .Select(layout => layout.LayoutKey)
            .ToArray();

        Assert.Empty(broken);
    }

    [Fact]
    public void TheColumnCountMatchesTheRegionCount()
    {
        // Zwei Regionen und drei Spalten wäre eine leere dritte, die niemand füllen kann; drei
        // Regionen und zwei Spalten stapelte die dritte irgendwohin. Beides sieht nach einem
        // Renderer-Fehler aus und ist eine Unstimmigkeit zwischen diesen beiden Dateien.
        var mismatched = new List<string>();

        foreach (var layout in SurfaceBaseSectionLayouts.All)
        {
            var rule = Regex.Match(
                Css,
                $@"\[data-cal-layout='{Regex.Escape(layout.LayoutKey)}'\]\s*\{{([^}}]*)\}}");
            Assert.True(rule.Success, $"Keine Regel für Layout '{layout.LayoutKey}'.");

            var columns = Regex.Match(rule.Groups[1].Value, @"grid-template-columns:\s*([^;]+);");
            Assert.True(columns.Success, $"Layout '{layout.LayoutKey}' setzt keine Spalten.");

            var count = CountColumns(columns.Groups[1].Value);
            if (count != layout.Regions.Count)
            {
                mismatched.Add($"{layout.LayoutKey}: {layout.Regions.Count} Regionen, {count} Spalten");
            }
        }

        Assert.Empty(mismatched);
    }

    [Fact]
    public void TheStylesheetComputesInTokensRatherThanFixedMeasures()
    {
        // Ein fester Pixelwert wäre genau die Freiheit, die die Token-Achse dem Editor
        // verweigert — mit dem Unterschied, dass sie niemand zurücknehmen kann. Die
        // Umbruchbreite ist ausgenommen: Sie steht in `em`, hängt also an der Schriftgröße und
        // nicht am Gerät.
        var withoutMediaQueries = Regex.Replace(Css, @"@media[^{]*\{", string.Empty);
        var fixedLengths = Regex.Matches(withoutMediaQueries, @":[^;]*?\d+(px|pt)\b")
            .Select(match => match.Value.Trim());

        Assert.Empty(fixedLengths);
    }

    [Fact]
    public void ARegionDoesNotBurstItsColumn()
    {
        // Die berüchtigte Grid-Mindestbreite von `auto`: Ohne `min-width: 0` sprengt eine
        // Tabelle oder ein <pre> seine Rasterspalte, und das Layout kippt für die ganze Sektion.
        Assert.Matches(@"\.cal-region\s*\{[^}]*min-width:\s*0", Css);
    }

    // ── Was ein Plugin-Theme davon erbt ──────────────────────────────────────

    [Fact]
    public void AThemeInheritsTheBaseAndAddsItsOwn()
    {
        // Die sichere Richtung: Das Basis-Stylesheet ist immer geladen, also funktionieren die
        // Basis-Layouts auch unter einem fremden Theme — und ein Theme, das nur eine Variante
        // beisteuern will, muss die ganze Palette nicht wiederholen.
        var own = new SectionLayoutDefinition("sidebar-right", "Seitenspalte rechts",
            [new("main", "Inhalt"), new("aside", "Seitenspalte")], 45);

        var composed = SurfaceBaseSectionLayouts.Compose([own], inherit: true);

        Assert.Contains(composed, layout => layout.LayoutKey == "single");
        Assert.Contains(composed, layout => layout.LayoutKey == "sidebar-right");
    }

    [Fact]
    public void AThemeLayoutDisplacesTheBaseOneOfTheSameName()
    {
        // Zusammenzuführen wäre der Weg, auf dem ein Theme ein `two-2-1` mit den Regionen der
        // Basis bekäme, obwohl sein CSS zwei andere kennt — und die Blöcke lägen dann in
        // Regionen, die es nicht gibt.
        var own = new SectionLayoutDefinition("two-2-1", "Anders",
            [new("links", "Links"), new("rechts", "Rechts")], 30);

        var composed = SurfaceBaseSectionLayouts.Compose([own], inherit: true);

        var replaced = Assert.Single(composed, layout => layout.LayoutKey == "two-2-1");
        Assert.Equal(["links", "rechts"], replaced.Regions.Select(region => region.RegionKey));
    }

    [Fact]
    public void AThemeThatDeclinesToInheritStandsAlone()
    {
        var own = new SectionLayoutDefinition("eigen", "Eigen", [new("main", "Inhalt")], 10);

        var composed = SurfaceBaseSectionLayouts.Compose([own], inherit: false);

        Assert.Equal(["eigen"], composed.Select(layout => layout.LayoutKey));
    }

    [Fact]
    public void ComposedLayoutsComeOutInDisplayOrder()
    {
        var own = new SectionLayoutDefinition("dazwischen", "Dazwischen", [new("main", "Inhalt")], 25);

        var composed = SurfaceBaseSectionLayouts.Compose([own], inherit: true);

        Assert.Equal(
            composed.Select(layout => layout.SortOrder).Order(),
            composed.Select(layout => layout.SortOrder));
    }

    /// <summary>
    /// Spalten in einer <c>grid-template-columns</c>-Angabe. Versteht die beiden Formen, die das
    /// Stylesheet benutzt: eine Aufzählung von <c>minmax(...)</c> und <c>repeat(n, …)</c>.
    /// </summary>
    private static int CountColumns(string value)
    {
        var repeat = Regex.Match(value, @"repeat\(\s*(\d+)\s*,");
        return repeat.Success
            ? int.Parse(repeat.Groups[1].Value)
            : Regex.Count(value, @"minmax\(");
    }

    private static string FindTokensScss()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(
                directory.FullName,
                "src", "Surface.Rendering", "Resources", "app", "surface", "src", "styles", "tokens.scss");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("tokens.scss wurde im Repository nicht gefunden.");
    }
}
