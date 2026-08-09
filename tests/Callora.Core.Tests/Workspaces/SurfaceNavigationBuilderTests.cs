using Callora.Core.Application.Workspaces;
using Callora.Core.Domain.Workspaces;
using Xunit;

namespace Callora.Core.Tests.Workspaces;

/// <summary>
/// Die Navigation einer Fläche (ADR-019 §5): die Kinder ihrer Wurzel, und nicht mehr.
/// </summary>
public sealed class SurfaceNavigationBuilderTests
{
    private static WorkspaceSurfaceSnapshot Node(
        string key,
        string segment,
        string? parentKey = null,
        int position = 0,
        bool isActive = true) =>
        new(
            Guid.NewGuid(),
            "acme",
            key,
            key,
            "spa",
            null,
            null,
            segment,
            SurfaceAuthentication.Public,
            SurfaceRouting.Tree,
            null,
            null,
            null,
            null,
            null,
            isActive,
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch)
        {
            ParentSurfaceKey = parentKey,
            Position = position,
        };

    private static IReadOnlyList<string> Paths(IReadOnlyList<SurfaceNavigationNode> nodes) =>
        nodes.SelectMany(node => new[] { node.Path }.Concat(Paths(node.Children))).ToArray();

    [Fact]
    public void TheNavigationIsTheChildrenOfTheRoot()
    {
        var portal = Node("portal", "/portal");
        var partner = Node("partner", "partner", "portal");
        var kontakt = Node("kontakt", "kontakt", "portal", position: 1);

        var nav = SurfaceNavigationBuilder.Build(portal, [portal, partner, kontakt]);

        Assert.Equal(["partner", "kontakt"], nav.Select(node => node.SurfaceKey));
    }

    [Fact]
    public void AChildSeesTheWholeApplicationAndNotOnlyItsOwnBranch()
    {
        // Wer auf /portal/partner steht, soll die Gliederung des Portals sehen. Nur den eigenen
        // Zweig zu zeigen hieße, dass man von einer Unterseite nie zu den Geschwistern
        // zurückfindet.
        var portal = Node("portal", "/portal");
        var partner = Node("partner", "partner", "portal");
        var kontakt = Node("kontakt", "kontakt", "portal", position: 1);

        var nav = SurfaceNavigationBuilder.Build(partner, [portal, partner, kontakt]);

        Assert.Equal(["partner", "kontakt"], nav.Select(node => node.SurfaceKey));
    }

    [Fact]
    public void TheTreeEndsAtTheNextRoot()
    {
        // Der Dialer ist eine andere Anwendung. Ihn einzublenden hieße, in einer Website auf
        // einen Arbeitsplatz zu verlinken, für den ganz andere Leute angemeldet sind.
        var portal = Node("portal", "/portal");
        var partner = Node("partner", "partner", "portal");
        var dialer = Node("dialer", "/dialer");
        var kampagnen = Node("kampagnen", "kampagnen", "dialer");

        var nav = SurfaceNavigationBuilder.Build(portal, [portal, partner, dialer, kampagnen]);

        Assert.Equal(["partner"], nav.Select(node => node.SurfaceKey));
    }

    [Fact]
    public void EachEntryCarriesItsFullPath()
    {
        // Das gespeicherte Segment allein wäre keine erreichbare Adresse — in ein href gehört
        // die ganze Kette.
        var portal = Node("portal", "/portal");
        var partner = Node("partner", "partner", "portal");
        var downloads = Node("downloads", "downloads", "partner");

        var nav = SurfaceNavigationBuilder.Build(portal, [portal, partner, downloads]);

        Assert.Equal(["/portal/partner", "/portal/partner/downloads"], Paths(nav));
    }

    [Fact]
    public void ChildrenComeOutInTheirDeclaredOrder()
    {
        var portal = Node("portal", "/portal");
        var b = Node("b", "b", "portal", position: 0);
        var a = Node("a", "a", "portal", position: 1);

        var nav = SurfaceNavigationBuilder.Build(portal, [portal, a, b]);

        Assert.Equal(["b", "a"], nav.Select(node => node.SurfaceKey));
    }

    [Fact]
    public void AnInactiveNodeIsLeftOut()
    {
        var portal = Node("portal", "/portal");
        var versteckt = Node("versteckt", "versteckt", "portal", isActive: false);

        Assert.Empty(SurfaceNavigationBuilder.Build(portal, [portal, versteckt]));
    }

    [Fact]
    public void ANodeWithoutALayoutStaysNavigable()
    {
        // Es ist dann eine Gliederungsebene, kein Fehler — eine Oberfläche darf es nur anders
        // darstellen als ein Ziel mit Inhalt.
        var portal = Node("portal", "/portal");
        var bereich = Node("bereich", "bereich", "portal");
        var seite = Node("seite", "seite", "bereich");

        var nav = SurfaceNavigationBuilder.Build(
            portal,
            [portal, bereich, seite],
            hasLayout: node => node.SurfaceKey == "seite");

        Assert.False(nav[0].HasLayout);
        Assert.True(nav[0].Children[0].HasLayout);
    }

    [Fact]
    public void ACycleInStoredDataDoesNotInflateTheNavigation()
    {
        // Sonst wüchse sie endlos — und zwar bei jedem Besucher, nicht nur bei dem, der den
        // Zyklus angelegt hat.
        var portal = Node("portal", "/portal");
        var a = Node("a", "a", "b");
        var b = Node("b", "b", "a");

        var nav = SurfaceNavigationBuilder.Build(portal, [portal, a, b]);

        // Weder a noch b hängen an der Wurzel — sie hängen aneinander. Die Navigation des
        // Portals bleibt leer statt endlos zu wachsen.
        Assert.Empty(nav);
    }

    [Fact]
    public void ARootWithoutChildrenHasAnEmptyNavigation()
    {
        var portal = Node("portal", "/portal");

        Assert.Empty(SurfaceNavigationBuilder.Build(portal, [portal]));
    }
}
