using Callora.Core.Application.Workspaces;
using Xunit;

namespace Callora.Core.Tests.Workspaces;

/// <summary>
/// Die Regeln des Surface-Baums (ADR-019). Sie stehen als reine Funktionen da, weil sie beides
/// sind — Bedingung beim Schreiben und Auflösung beim Lesen; hier werden sie einzeln geprüft.
/// </summary>
public sealed class SurfaceTreeTests
{
    private sealed record Node(Guid Id, Guid? ParentId, string? Value);

    private static (Guid Root, Guid Child, Guid Grandchild, Dictionary<Guid, Node> ById) Chain()
    {
        var root = new Node(Guid.NewGuid(), null, "root");
        var child = new Node(Guid.NewGuid(), root.Id, null);
        var grandchild = new Node(Guid.NewGuid(), child.Id, null);

        return (root.Id, child.Id, grandchild.Id, new Dictionary<Guid, Node>
        {
            [root.Id] = root,
            [child.Id] = child,
            [grandchild.Id] = grandchild,
        });
    }

    // ── Zyklen ──────────────────────────────────────────────────────────────

    [Fact]
    public void ANodeCannotBecomeItsOwnParent()
    {
        // Der kürzeste Zyklus — und der, den eine Prüfung übersähe, die erst beim Vorfahren
        // anfinge.
        var id = Guid.NewGuid();

        Assert.True(SurfaceTree.WouldCreateCycle(id, id, new Dictionary<Guid, Guid?>()));
    }

    [Fact]
    public void ANodeCannotBecomeAChildOfItsOwnDescendant()
    {
        var (root, child, grandchild, byId) = Chain();
        var parents = byId.ToDictionary(pair => pair.Key, pair => pair.Value.ParentId);

        Assert.True(SurfaceTree.WouldCreateCycle(root, grandchild, parents));
        Assert.True(SurfaceTree.WouldCreateCycle(root, child, parents));
    }

    [Fact]
    public void MovingWithinTheTreeIsAllowedWhereItCreatesNoCycle()
    {
        var (root, _, grandchild, byId) = Chain();
        var parents = byId.ToDictionary(pair => pair.Key, pair => pair.Value.ParentId);
        var newcomer = Guid.NewGuid();

        Assert.False(SurfaceTree.WouldCreateCycle(newcomer, grandchild, parents));
        Assert.False(SurfaceTree.WouldCreateCycle(grandchild, root, parents));
    }

    [Fact]
    public void BecomingARootIsAlwaysAllowed()
    {
        var (root, _, _, byId) = Chain();
        var parents = byId.ToDictionary(pair => pair.Key, pair => pair.Value.ParentId);

        Assert.False(SurfaceTree.WouldCreateCycle(root, null, parents));
    }

    [Fact]
    public void AChainDeeperThanTheLimitIsRefused()
    {
        // Keine fachliche Grenze, sondern eine Reißleine: Die Kette wird bei jeder Anfrage
        // durchlaufen. Eine Struktur, die irrtümlich sehr tief geworden ist, soll den
        // Renderpfad nicht verlangsamen.
        var parents = new Dictionary<Guid, Guid?>();
        var ids = Enumerable.Range(0, SurfaceTree.MaxDepth + 5).Select(_ => Guid.NewGuid()).ToArray();
        for (var i = 1; i < ids.Length; i++)
        {
            parents[ids[i]] = ids[i - 1];
        }

        parents[ids[0]] = null;

        Assert.True(SurfaceTree.WouldCreateCycle(Guid.NewGuid(), ids[^1], parents));
    }

    // ── Die Kette ───────────────────────────────────────────────────────────

    [Fact]
    public void AncestryRunsFromTheNodeToTheRoot()
    {
        var (root, child, grandchild, byId) = Chain();

        var chain = SurfaceTree.AncestryOf(
            byId[grandchild], node => node.Id, node => node.ParentId, byId);

        Assert.Equal([grandchild, child, root], chain.Select(node => node.Id));
    }

    [Fact]
    public void AncestryEndsWhereAnAncestorIsMissing()
    {
        // Etwa weil ein Mandantenfilter ihn ausschloss. Der Knoten gilt dann als Wurzel — eine
        // Fläche ohne geerbtes Theme, kein Fehler beim Besucher.
        var orphanParent = Guid.NewGuid();
        var node = new Node(Guid.NewGuid(), orphanParent, null);
        var byId = new Dictionary<Guid, Node> { [node.Id] = node };

        var chain = SurfaceTree.AncestryOf(node, n => n.Id, n => n.ParentId, byId);

        Assert.Single(chain);
    }

    [Fact]
    public void AncestryDoesNotHangOnACycleInStoredData()
    {
        // Aus einer Migration, einem direkten SQL-Eingriff. Eine abgeschnittene Kette gibt eine
        // sichtbar falsche Seite; eine hängende Anfrage gibt gar nichts.
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var byId = new Dictionary<Guid, Node>
        {
            [a] = new(a, b, null),
            [b] = new(b, a, null),
        };

        var chain = SurfaceTree.AncestryOf(byId[a], node => node.Id, node => node.ParentId, byId);

        Assert.Equal(2, chain.Count);
    }

    // ── Vererbung ───────────────────────────────────────────────────────────

    [Fact]
    public void TheFirstSetValueAlongTheChainWins()
    {
        var chain = new[]
        {
            new Node(Guid.NewGuid(), null, null),
            new Node(Guid.NewGuid(), null, "mitte"),
            new Node(Guid.NewGuid(), null, "wurzel"),
        };

        Assert.Equal("mitte", SurfaceTree.Inherited(chain, node => node.Value));
    }

    [Fact]
    public void AnEmptyStringDoesNotCountAsSet()
    {
        // Ein leeres Feld ist in einer Verwaltungsoberfläche dasselbe wie ein nicht
        // ausgefülltes. Es als eigenen Wert zu werten hieße, dass ein versehentlich geleertes
        // Feld die Vererbung abschaltet — und die Fläche dann gar kein Theme hätte.
        var chain = new[]
        {
            new Node(Guid.NewGuid(), null, "   "),
            new Node(Guid.NewGuid(), null, "wurzel"),
        };

        Assert.Equal("wurzel", SurfaceTree.Inherited(chain, node => node.Value));
    }

    [Fact]
    public void InheritedFromNamesTheNodeSoPairsStayTogether()
    {
        // Plugin-Id und Version einzeln zu suchen ergäbe im schlimmsten Fall das Theme des
        // einen Vorfahren mit der Version eines anderen — eine Zuweisung, die es nie gab.
        var middle = new Node(Guid.NewGuid(), null, "mitte");
        var chain = new[] { new Node(Guid.NewGuid(), null, null), middle };

        Assert.Same(middle, SurfaceTree.InheritedFrom(chain, node => node.Value));
    }

    // ── Der Pfad ────────────────────────────────────────────────────────────

    [Fact]
    public void ThePathIsComposedFromRootToNode()
    {
        // Die Kette kommt von innen nach außen, der Pfad entsteht andersherum.
        Assert.Equal("/portal/partner/downloads", SurfaceTree.ComposePath(
            ["downloads", "partner", "/portal"]));
    }

    [Fact]
    public void SegmentsAreNormalizedRatherThanConcatenated()
    {
        // Ein Segment kann mit oder ohne Schrägstrich eingegeben werden; beides muss denselben
        // Pfad ergeben, sonst hängt die URL daran, wie jemand ein Feld ausgefüllt hat.
        Assert.Equal("/portal/partner", SurfaceTree.ComposePath(["/partner/", "portal/"]));
    }

    [Fact]
    public void ARootAtTheOriginStaysAtTheOrigin()
    {
        Assert.Equal("/", SurfaceTree.ComposePath(["/"]));
        Assert.Equal("/", SurfaceTree.ComposePath([null]));
        Assert.Equal("/kontakt", SurfaceTree.ComposePath(["kontakt", "/"]));
    }
}
