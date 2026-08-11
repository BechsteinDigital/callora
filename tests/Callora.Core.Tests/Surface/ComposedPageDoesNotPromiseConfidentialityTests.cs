using Xunit;

namespace Callora.Core.Tests.Surface;

/// <summary>
/// Die ausgelieferte Seite verspricht keine Vertraulichkeit, die der Renderpfad nicht herstellt.
/// <para>
/// <c>page/composed.njk</c> sagte zu, die Komposition komme „with every confidential control left
/// out". Der Renderer kann das — <c>SurfaceBlockPropsSerializer</c> lässt gemeldete Controls aus
/// —, aber die Meldung kam nie: <c>confidential</c> steht ausschließlich im Browser-Vertrag der
/// Blöcke, und serverseitig gibt es keine Blockbeschreibung, aus der der Host es lesen könnte.
/// Der einzige Produktionsaufruf setzte den Parameter deshalb nicht.
/// </para>
/// <para>
/// Dieser Test hält die beiden Aussagen aneinander: Solange der Renderpfad keine Quelle für
/// vertrauliche Controls übergibt, darf die Vorlage es nicht zusagen — und sobald jemand sie
/// verdrahtet, fällt hier auf, dass die Zusage wieder gemacht werden darf. Veraltete
/// Dokumentation ist schlimmer als gar keine: Sie klingt plausibel, und man glaubt ihr.
/// </para>
/// </summary>
public sealed class ComposedPageDoesNotPromiseConfidentialityTests
{
    [Fact]
    public void ThePromiseAndTheWiringAgree()
    {
        var wired = Source("src", "Surface.Rendering", "Api", "SurfaceRenderEndpoints.cs")
            .Contains("confidentialControls:", StringComparison.Ordinal);

        var promised = Source("src", "Surface.Rendering", "Resources", "views", "surface", "page", "composed.njk")
            .Contains("confidential control", StringComparison.OrdinalIgnoreCase);

        Assert.True(
            wired == promised,
            wired
                ? "Der Renderpfad filtert vertrauliche Controls inzwischen — page/composed.njk darf "
                  + "das wieder zusagen."
                : "page/composed.njk sagt zu, vertrauliche Controls auszulassen; der Renderpfad "
                  + "übergibt dafür aber keine Quelle. Entweder verdrahten oder die Zusage streichen.");
    }

    private static string Source(params string[] segments)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Callora.Host.sln")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return File.ReadAllText(Path.Combine([directory!.FullName, .. segments]));
    }
}
