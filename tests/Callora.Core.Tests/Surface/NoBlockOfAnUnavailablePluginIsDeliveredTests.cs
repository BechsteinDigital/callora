using Callora.Surface.Rendering.Api;
using Xunit;

namespace Callora.Core.Tests.Surface;

/// <summary>
/// Ein Block, dessen Plugin nicht verfügbar ist, gehört nicht ins ausgelieferte HTML.
/// <para>
/// Der Renderpfad konnte das schon — er sagte es nur nie: <c>blockIsAvailable</c> war ein
/// Konstruktorparameter, den ausschließlich Tests setzten, während der einzige Produktionsaufruf
/// beim Default <c>_ =&gt; true</c> blieb. Die Insel blieb tot, weil das JS des Plugins gar nicht
/// erst geladen wurde; im HTML stand aber weiter die vom Operator gespeicherte Konfiguration
/// (<c>data-callora-props</c>) eines Plugins, das dieser Workspace nicht mehr haben darf.
/// </para>
/// </summary>
public sealed class NoBlockOfAnUnavailablePluginIsDeliveredTests
{
    private static readonly string[] Chain = ["communication", "content"];

    [Fact]
    public void ABlockOfAPluginInTheChainIsDelivered()
    {
        Assert.True(SurfaceContributors.BlockIsAvailable(Chain)("communication.incoming-call"));
    }

    [Fact]
    public void ABlockOfAPluginOutsideTheChainIsNot()
    {
        // Die Kette ist über IPluginAvailabilityEvaluator gefiltert: Wer hier fehlt, ist
        // deinstalliert, abgeschaltet oder für diesen Workspace nicht berechtigt.
        Assert.False(SurfaceContributors.BlockIsAvailable(Chain)("billing.invoice-list"));
    }

    [Fact]
    public void TheBlockIdCarriesItsPluginBeforeTheFirstDot()
    {
        // Dieselbe Konvention, nach der die Block-Registry im Browser sortiert — auch bei
        // mehreren Punkten entscheidet das erste Segment.
        Assert.True(SurfaceContributors.BlockIsAvailable(Chain)("content.hero.wide"));
        Assert.False(SurfaceContributors.BlockIsAvailable(Chain)("shop.hero.wide"));
    }

    [Fact]
    public void APluginIdWithoutADotIsTheBlockIdItself()
    {
        Assert.True(SurfaceContributors.BlockIsAvailable(Chain)("content"));
        Assert.False(SurfaceContributors.BlockIsAvailable(Chain)("shop"));
    }

    [Fact]
    public void CasingDoesNotDecideAvailability()
    {
        Assert.True(SurfaceContributors.BlockIsAvailable(Chain)("Communication.Incoming-Call"));
    }

    [Fact]
    public void AnEmptyBlockIdIsNeverAvailable()
    {
        var available = SurfaceContributors.BlockIsAvailable(Chain);

        Assert.False(available(string.Empty));
        Assert.False(available("   "));
    }

    [Fact]
    public void AnEmptyChainDeliversNothing()
    {
        // Fail-closed: Eine leere Kette heißt „kein Plugin ist hier verfügbar", nicht „prüf nicht".
        Assert.False(SurfaceContributors.BlockIsAvailable([])("communication.incoming-call"));
    }
}
