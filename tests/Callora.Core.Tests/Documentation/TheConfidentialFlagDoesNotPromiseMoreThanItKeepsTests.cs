using Callora.Core.Tests.Cli;
using Xunit;

namespace Callora.Core.Tests.Documentation;

/// <summary>
/// Bindet die Zusage im Block-Vertrag an das, was der Renderpfad tatsächlich tut.
/// </summary>
/// <remarks>
/// <para>
/// Der Anlass (#290): <c>block-contract.ts</c> sagte Block-Autoren zu, <c>confidential</c> halte
/// den Wert aus dem ausgelieferten Markup heraus — „für einen API-Key, eine interne Id". Der
/// Renderpfad tut das nicht: <c>SurfaceCompositionRenderer</c> nimmt eine Menge vertraulicher
/// Steuerelemente entgegen und würde filtern, aber niemand füllt sie, weil es serverseitig keine
/// Blockbeschreibung gibt. Der C#-Kommentar sagte das seit jeher offen; der TypeScript-Vertrag
/// behauptete das Gegenteil, und er ist der, den ein Block-Autor liest.
/// </para>
/// <para>
/// Geprüft wird in <b>beide</b> Richtungen, und das ist der Punkt. Dass der Vertrag nichts
/// verspricht, solange nichts verdrahtet ist, fängt den heutigen Zustand. Dass er die Einschränkung
/// wieder verliert, sobald jemand die Filterung verdrahtet, fängt den umgekehrten Fehler — eine
/// Warnung, die stehen bleibt, nachdem sie unwahr geworden ist, kostet später genauso viel
/// Vertrauen wie eine Zusage, die nie stimmte.
/// </para>
/// </remarks>
public sealed class TheConfidentialFlagDoesNotPromiseMoreThanItKeepsTests
{
    private static readonly string Root = ScaffoldedPluginFixture.ResolveRepositoryRoot();

    private static readonly string ContractPath = Path.Combine(
        Root, "src", "Surface.Rendering", "Resources", "app", "surface", "src", "blocks", "block-contract.ts");

    private static readonly string EndpointsPath = Path.Combine(
        Root, "src", "Surface.Rendering", "Api", "SurfaceRenderEndpoints.cs");

    [Fact]
    public void TheContractWarnsExactlyWhileTheFilterIsUnwired()
    {
        var contract = File.ReadAllText(ContractPath);
        var wired = IsConfidentialFilterWired();

        if (wired)
        {
            Assert.False(
                contract.Contains("not a guarantee", StringComparison.OrdinalIgnoreCase),
                "Die Filterung vertraulicher Steuerelemente ist inzwischen verdrahtet — "
                + "SurfaceRenderEndpoints übergibt confidentialControls. Damit ist die "
                + "Einschränkung in block-contract.ts unwahr geworden und gehört entfernt: Eine Warnung, "
                + "die nach ihrer Zeit stehen bleibt, führt Block-Autoren genauso in die Irre wie die "
                + "Zusage, die vorher dort stand.");
            return;
        }

        Assert.True(
            contract.Contains("not a guarantee", StringComparison.OrdinalIgnoreCase),
            "block-contract.ts sagt zu confidential etwas zu, das der Renderpfad nicht hält: "
            + "SurfaceRenderEndpoints konstruiert SurfaceCompositionRenderer ohne confidentialControls, "
            + "also wird nichts gefiltert und jeder Wert geht so aus, wie er hineingeschrieben wurde. "
            + "Wer einen Schlüssel dahinter legt, liefert ihn an jeden Besucher aus. Entweder die "
            + "Einschränkung bleibt im Vertrag stehen, oder die Filterung wird verdrahtet.");
    }

    /// <summary>
    /// Die Gegenprobe zur Erkennung selbst: Der Anschluss existiert überhaupt. Verschwände der
    /// Parameter, meldete der Test oben dauerhaft „nicht verdrahtet" — und wäre damit still, ohne
    /// dass jemand die Filterung je wieder anschließen könnte.
    /// </summary>
    [Fact]
    public void TheRendererStillOffersTheConnectionPoint()
    {
        var renderer = File.ReadAllText(Path.Combine(
            Root, "src", "Surface.Rendering", "Rendering", "Composition", "SurfaceCompositionRenderer.cs"));

        Assert.Contains("confidentialControls", renderer, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ob die Composition-Root die Filterung versorgt. Gelesen statt aufgerufen: Die Verdrahtung ist
    /// eine Konstruktoreingabe in einer statischen Endpunktklasse, und sie zu erreichen hieße, den
    /// halben Renderpfad aufzubauen — für eine Aussage, die aus einer Zeile Text folgt.
    /// </summary>
    private static bool IsConfidentialFilterWired()
    {
        var endpoints = File.ReadAllText(EndpointsPath);
        return endpoints.Contains("confidentialControls:", StringComparison.Ordinal);
    }
}
