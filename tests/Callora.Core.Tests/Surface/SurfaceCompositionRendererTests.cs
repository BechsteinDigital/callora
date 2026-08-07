using Callora.Core.Application.Surfaces.Layout;
using Callora.Surface.Rendering.Rendering.Composition;
using Xunit;

namespace Callora.Core.Tests.Surface;

/// <summary>
/// What a composed layout turns into. The security-relevant part is what does NOT come out:
/// <c>data-callora-props</c> stands in the delivered HTML and is readable by anyone who fetches
/// the page — on a Public surface without signing in (design §7.5, Regel 11).
/// </summary>
public sealed class SurfaceCompositionRendererTests
{
    private static SurfaceLayoutDocument Document(params SurfaceLayoutSection[] sections) =>
        new("portal-layout", 3, sections);

    private static SurfaceLayoutSection Section(
        params SurfaceLayoutBlock[] blocks) =>
        new("single", 0, blocks);

    private static SurfaceLayoutBlock Block(
        string id,
        int position = 0,
        string region = "main",
        params (string Name, SurfaceBlockBinding Binding)[] config) =>
        new(id, region, position,
            config.ToDictionary(c => c.Name, c => c.Binding, StringComparer.Ordinal));

    private static SurfaceBlockBinding Static(object value) =>
        new(SurfaceBlockBinding.StaticSource, Value: value);

    private static SurfaceBlockBinding Context(string key, string? path = null) =>
        new(SurfaceBlockBinding.ContextSource, Key: key, Path: path);

    [Fact]
    public void ABlockBecomesTheIslandTheRuntimeAlreadyUnderstands()
    {
        var html = new SurfaceCompositionRenderer().Render(
            Document(Section(Block("communication.call-list"))));

        Assert.Contains("data-callora-island=\"communication.call-list\"", html, StringComparison.Ordinal);
        Assert.Contains("class=\"callora-island\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public void StaticConfigurationTravelsAsProps()
    {
        var html = new SurfaceCompositionRenderer().Render(
            Document(Section(Block(
                "communication.call-list",
                config: [("title", Static("Aktive Anrufe")), ("max", Static(5))]))));

        Assert.Contains("data-callora-props=", html, StringComparison.Ordinal);
        Assert.Contains("Aktive Anrufe", html, StringComparison.Ordinal);
        Assert.Contains("&quot;max&quot;:5", html, StringComparison.Ordinal);
    }

    [Fact]
    public void AContextBindingTravelsAsABindingAndNeverAsAResolvedValue()
    {
        // Aufgelöst hier stünde der aktuelle Wert im Quelltext der Seite — für jeden Besucher,
        // gleich wer ihn sehen darf. Der Browser löst ihn gegen den Kanal auf, wo die
        // Projektion bereits entschieden hat, was DIESE Person bekommt.
        var html = new SurfaceCompositionRenderer().Render(
            Document(Section(Block(
                "communication.call-detail",
                config: [("call", Context("communication.active-call/v1", "customer.name"))]))));

        Assert.Contains("__context", html, StringComparison.Ordinal);
        Assert.Contains("communication.active-call/v1", html, StringComparison.Ordinal);
        Assert.Contains("customer.name", html, StringComparison.Ordinal);
    }

    [Fact]
    public void AConfidentialControlNeverReachesTheAttribute()
    {
        var renderer = new SurfaceCompositionRenderer(
            confidentialControls: _ => new HashSet<string>(StringComparer.Ordinal) { "apiKey" });

        var html = renderer.Render(Document(Section(Block(
            "crm.lookup",
            config: [("apiKey", Static("geheim-123")), ("title", Static("Suche"))]))));

        Assert.DoesNotContain("geheim-123", html, StringComparison.Ordinal);
        Assert.Contains("Suche", html, StringComparison.Ordinal);
    }

    [Fact]
    public void ABlockWithNothingVisibleCarriesNoAttributeAtAll()
    {
        var renderer = new SurfaceCompositionRenderer(
            confidentialControls: _ => new HashSet<string>(StringComparer.Ordinal) { "apiKey" });

        var html = renderer.Render(Document(Section(Block(
            "crm.lookup", config: [("apiKey", Static("geheim"))]))));

        Assert.DoesNotContain("data-callora-props", html, StringComparison.Ordinal);
    }

    [Fact]
    public void AnOrphanedBlockIsLeftOutRatherThanRenderedAsAHole()
    {
        // Plugin deinstalliert. Das Layout bleibt intakt und wird wieder vollständig, sobald das
        // Plugin zurückkommt — der Besucher sieht in der Zwischenzeit keine Lücke mit Fehlertext.
        var renderer = new SurfaceCompositionRenderer(
            blockIsAvailable: id => id != "verschwunden.block");

        var html = renderer.Render(Document(Section(
            Block("verschwunden.block"),
            Block("crm.lead-list", position: 1))));

        Assert.DoesNotContain("verschwunden.block", html, StringComparison.Ordinal);
        Assert.Contains("crm.lead-list", html, StringComparison.Ordinal);
    }

    [Fact]
    public void SectionsAndBlocksComeOutInTheirDeclaredOrder()
    {
        var html = new SurfaceCompositionRenderer().Render(new SurfaceLayoutDocument(
            "l", 1,
            [
                new("single", 1, [Block("zweiter")]),
                new("single", 0, [Block("erster")]),
            ]));

        Assert.True(
            html.IndexOf("erster", StringComparison.Ordinal) <
            html.IndexOf("zweiter", StringComparison.Ordinal));
    }

    [Fact]
    public void ASectionCarriesTokenStepsRatherThanValues()
    {
        // Die Guardrail: eine Sektion kann luftiger sein, aber niemand kann hier 37 Pixel
        // hineinschreiben.
        var html = new SurfaceCompositionRenderer().Render(Document(
            new SurfaceLayoutSection("two-2-1", 0, [Block("x")], Spacing: "lg", SurfaceRole: "raised")));

        Assert.Contains("data-cal-layout=\"two-2-1\"", html, StringComparison.Ordinal);
        Assert.Contains("data-cal-spacing=\"lg\"", html, StringComparison.Ordinal);
        Assert.Contains("data-cal-surface=\"raised\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public void BlocksAreGroupedByRegion()
    {
        var html = new SurfaceCompositionRenderer().Render(Document(
            new SurfaceLayoutSection("two-2-1", 0,
            [
                Block("haupt", region: "main"),
                Block("rand", region: "aside"),
            ])));

        Assert.Contains("data-cal-region=\"main\"", html, StringComparison.Ordinal);
        Assert.Contains("data-cal-region=\"aside\"", html, StringComparison.Ordinal);
    }

    // ── Wenn das Theme das Layout nicht mehr kennt (§7.8) ───────────────────

    [Fact]
    public void ALayoutTheThemeNoLongerKnowsFallsBackWithoutLosingAnything()
    {
        // Nach einem Theme-Wechsel benennt `data-cal-layout="two-2-1"` ein Raster, das niemand
        // stylt — die Sektion zerfiele in das, was der Browser mit ungestylten Divs tut. Der
        // Rückfall stellt die Blöcke in eine lesbare Spalte: schlichter, aber vollständig.
        var document = Document(new SurfaceLayoutSection("two-2-1", 0, [
            Block("haupt", region: "main"),
            Block("rand", region: "aside"),
        ]));

        var html = new SurfaceCompositionRenderer(layoutIsKnown: key => key == "single")
            .Render(document);

        Assert.Contains("data-cal-layout=\"single\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("two-2-1", html, StringComparison.Ordinal);
        // Beide Blöcke sind noch da — der Rückfall betrifft das Raster, nicht den Inhalt.
        Assert.Contains("data-callora-island=\"haupt\"", html, StringComparison.Ordinal);
        Assert.Contains("data-callora-island=\"rand\"", html, StringComparison.Ordinal);
        Assert.Contains("data-cal-region=\"aside\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public void ALayoutTheThemeStillKnowsIsLeftAlone()
    {
        var html = new SurfaceCompositionRenderer(layoutIsKnown: _ => true)
            .Render(Document(new SurfaceLayoutSection("two-2-1", 0, [Block("haupt")])));

        Assert.Contains("data-cal-layout=\"two-2-1\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public void WithoutAnOpinionAboutLayoutsNothingIsReplaced()
    {
        // Der Standardwert. Ein Renderer ohne Layout-Wissen darf nicht so tun, als hätte ein
        // Theme das Layout abgelehnt — das wäre eine sichtbare Änderung aus einem
        // Nicht-Ereignis.
        var html = new SurfaceCompositionRenderer()
            .Render(Document(new SurfaceLayoutSection("erfunden", 0, [Block("haupt")])));

        Assert.Contains("data-cal-layout=\"erfunden\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public void AStoredStringCannotBreakOutOfItsAttribute()
    {
        // Ein Operator ist vertrauenswürdig, eine gespeicherte Zeichenkette nicht.
        var html = new SurfaceCompositionRenderer().Render(Document(Section(Block(
            "x\"><script>alert(1)</script>",
            config: [("title", Static("\"><img onerror=alert(1)>"))]))));

        Assert.DoesNotContain("<script>alert(1)</script>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<img onerror", html, StringComparison.Ordinal);
    }
}
