namespace Callora.Core.Application.Extensions;

/// <summary>
/// Die Sektionslayouts, die das Basis-Surface-Theme mitbringt — und was ein Plugin-Theme davon
/// erbt.
/// <para>
/// Sektionslayouts gehören dem Theme (§7.1); das bleibt so. Die Basis ist ein Theme wie jedes
/// andere, nur eben das, das immer da ist — dieselbe Rolle, die <c>tokens.scss</c> für Farben
/// und Abstände spielt. Ohne sie hätte eine frische Installation nichts, womit sich etwas
/// komponieren ließe: Der Renderer gäbe <c>data-cal-layout</c> aus, der Editor böte keine Wahl,
/// und beides sähe nach einem Fehler aus statt nach einem fehlenden Theme.
/// </para>
/// <para>
/// <b>Ein Plugin-Theme erbt sie standardmäßig und ergänzt die eigenen.</b> Das ist die sichere
/// Richtung: Das Basis-Stylesheet der Runtime ist immer geladen, also funktionieren die
/// Basis-Layouts auch unter einem fremden Theme, und ein Theme, das nur <c>sidebar-right</c>
/// beisteuern will, muss nicht die ganze Palette wiederholen. Wer stattdessen ein eigenes
/// Rastersystem durchsetzen will, setzt <c>inheritSectionLayouts</c> auf <c>false</c> und steht
/// dann allein für alles gerade, was seine Layouts brauchen.
/// </para>
/// <para>
/// Die Liste steht hier und nicht im TypeScript-Paket, obwohl das CSS dort liegt: Der
/// Kompositions-Renderer muss dieselbe Vererbung kennen wie der Editor, und zwei Listen wären
/// zwei Wahrheiten. <c>SurfaceBaseSectionLayoutsTests</c> hält diese gegen
/// <c>tokens.scss</c> — ein deklariertes Layout ohne Regel wäre ein Angebot, das nichts tut.
/// </para>
/// </summary>
public static class SurfaceBaseSectionLayouts
{
    /// <summary>Was die Basis anbietet, in Anzeigereihenfolge.</summary>
    public static IReadOnlyList<SectionLayoutDefinition> All { get; } =
    [
        new("single", "Eine Spalte", [new("main", "Inhalt")], 10),
        new("two-1-1", "Zwei gleiche Spalten",
            [new("main", "Links"), new("secondary", "Rechts")], 20),
        new("two-2-1", "Zwei Spalten (2:1)",
            [new("main", "Inhalt"), new("aside", "Seitenspalte")], 30),
        // Die Seitenspalte steht ZUERST — die deklarierte Reihenfolge ist die Lesereihenfolge,
        // und im Raster liegt sie links.
        new("sidebar-left", "Seitenspalte links",
            [new("aside", "Seitenspalte"), new("main", "Inhalt")], 40),
        new("three-1-1-1", "Drei gleiche Spalten",
            [new("main", "Links"), new("secondary", "Mitte"), new("tertiary", "Rechts")], 50),
    ];

    /// <summary>
    /// Was ein Theme insgesamt anbietet: die Basis plus seine eigenen, oder nur seine eigenen.
    /// <para>
    /// Ein Layout des Themes verdrängt das gleichnamige der Basis vollständig. Zusammenzuführen
    /// wäre der Weg, auf dem ein Theme ein `two-2-1` mit den Regionen der Basis bekäme, obwohl
    /// sein CSS zwei andere kennt — und die Blöcke lägen dann in Regionen, die es nicht gibt.
    /// </para>
    /// </summary>
    public static IReadOnlyList<SectionLayoutDefinition> Compose(
        IReadOnlyList<SectionLayoutDefinition> themeLayouts,
        bool inherit)
    {
        ArgumentNullException.ThrowIfNull(themeLayouts);

        if (!inherit)
        {
            return themeLayouts;
        }

        var own = themeLayouts
            .Select(layout => layout.LayoutKey)
            .ToHashSet(StringComparer.Ordinal);

        return All
            .Where(layout => !own.Contains(layout.LayoutKey))
            .Concat(themeLayouts)
            .OrderBy(layout => layout.SortOrder)
            .ThenBy(layout => layout.LayoutKey, StringComparer.Ordinal)
            .ToArray();
    }
}
