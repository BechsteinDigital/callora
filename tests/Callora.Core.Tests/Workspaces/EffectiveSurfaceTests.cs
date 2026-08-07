using Callora.Core.Application.Workspaces;
using Callora.Core.Domain.Workspaces;
using Xunit;

namespace Callora.Core.Tests.Workspaces;

/// <summary>
/// Was für einen Knoten gilt, wenn er von seinen Vorfahren erbt (ADR-019 §3/§4).
/// <para>
/// Die beiden Tests am Ende tragen Sicherheitsgewicht: Wo die Anmeldung herkommt und ob ein
/// Kind den Zugriff lockern darf, ist keine Bequemlichkeitsfrage.
/// </para>
/// </summary>
public sealed class EffectiveSurfaceTests
{
    private static WorkspaceSurface Node(
        string key,
        string? pathSegment = null,
        string? host = null,
        SurfaceAccessMode accessMode = SurfaceAccessMode.Mixed,
        string? themePluginId = null,
        string? themeVersion = null,
        string? identityPluginId = null,
        string? locale = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            SurfaceKey = key,
            PublicPathPrefix = pathSegment ?? "/",
            PublicHost = host,
            AccessMode = accessMode,
            ThemePluginId = themePluginId,
            ThemeVersion = themeVersion,
            IdentityPluginId = identityPluginId,
            IdentityVersion = identityPluginId is null ? null : "1.0.0",
            Locale = locale,
            Workspace = new Callora.Core.Domain.Workspaces.Workspace { WorkspaceKey = "acme" },
        };

    [Fact]
    public void AChildInheritsHostThemeAndLocaleFromItsRoot()
    {
        var root = Node("portal", "/portal", "kunde.example", themePluginId: "theme.acme",
            themeVersion: "2.1.0", locale: "de-DE");
        var child = Node("partner", "partner");

        var effective = EffectiveSurface.From([child, root]);

        Assert.Equal("kunde.example", effective.PublicHost);
        Assert.Equal("theme.acme", effective.ThemePluginId);
        Assert.Equal("2.1.0", effective.ThemeVersion);
        Assert.Equal("de-DE", effective.Locale);
        Assert.Equal("/portal/partner", effective.PublicPathPrefix);
    }

    [Fact]
    public void AChildOverridesWhatItSetsItself()
    {
        var root = Node("portal", "/portal", themePluginId: "theme.acme", themeVersion: "2.1.0");
        var child = Node("shop", "shop", themePluginId: "theme.shop", themeVersion: "1.0.0");

        var effective = EffectiveSurface.From([child, root]);

        Assert.Equal("theme.shop", effective.ThemePluginId);
        Assert.Equal("1.0.0", effective.ThemeVersion);
    }

    [Fact]
    public void PluginIdAndVersionAlwaysComeFromTheSameNode()
    {
        // Einzeln gesucht ergäbe das Theme des einen Vorfahren mit der Version eines anderen —
        // eine Zuweisung, die es nie gab, und ein Fehler, der erst beim Laden des falschen
        // Bundles auffiele.
        var root = Node("portal", "/portal", themePluginId: "theme.acme", themeVersion: "2.1.0");
        var middle = Node("bereich", "bereich", themePluginId: "theme.bereich", themeVersion: null);
        var leaf = Node("seite", "seite");

        var effective = EffectiveSurface.From([leaf, middle, root]);

        Assert.Equal("theme.bereich", effective.ThemePluginId);
        // NICHT "2.1.0" von der Wurzel: Das Theme kommt von `middle`, also auch seine Version.
        Assert.Null(effective.ThemeVersion);
    }

    [Fact]
    public void ThePathIsBuiltFromTheChainAndNotStoredWhole()
    {
        // Ein Kind trägt sein Segment. Sonst müsste beim Verschieben eines Teilbaums jeder
        // Nachfahre umgeschrieben werden, und jeder übersehene wäre eine tote URL.
        var root = Node("portal", "/portal");
        var middle = Node("partner", "partner");
        var leaf = Node("downloads", "downloads");

        Assert.Equal("/portal/partner/downloads", EffectiveSurface.From([leaf, middle, root]).PublicPathPrefix);
    }

    [Fact]
    public void ARootResolvesToItself()
    {
        var root = Node("portal", "/portal", "kunde.example", themePluginId: "theme.acme");

        var effective = EffectiveSurface.From([root]);

        Assert.Equal(root.Id, effective.RootId);
        Assert.Equal("/portal", effective.PublicPathPrefix);
    }

    // ── Zugriff und Identität (§4) ──────────────────────────────────────────

    [Fact]
    public void TheAccessModeIsTheNodesOwnInBothDirections()
    {
        // Ein öffentliches Impressum unter einem angemeldeten Portal ist genauso legitim wie ein
        // geschützter Partnerbereich unter einer offenen Website. Eine Regel „nur verschärfen"
        // erzwänge den ersten Fall an eine eigene Wurzel — mit anderem Theme und anderer
        // Navigation.
        var strictRoot = Node("portal", "/portal", accessMode: SurfaceAccessMode.Authenticated);
        var openChild = Node("impressum", "impressum", accessMode: SurfaceAccessMode.Public);

        Assert.Equal(SurfaceAccessMode.Public, EffectiveSurface.From([openChild, strictRoot]).AccessMode);

        var openRoot = Node("web", "/", accessMode: SurfaceAccessMode.Public);
        var strictChild = Node("partner", "partner", accessMode: SurfaceAccessMode.Authenticated);

        Assert.Equal(
            SurfaceAccessMode.Authenticated,
            EffectiveSurface.From([strictChild, openRoot]).AccessMode);
    }

    [Fact]
    public void TheIdentityProviderComesFromTheRootAndNowhereElse()
    {
        // Die Session-Grenze ist deckungsgleich mit der Anwendungsgrenze. Ein Realm mitten im
        // Baum ließe eine Anmeldung enden, ohne dass die URL es verriete — und die Frage „bin
        // ich hier angemeldet?" hinge an der Vererbungskette statt an der Anwendung.
        var root = Node("portal", "/portal", identityPluginId: "identity.kunde");
        var child = Node("partner", "partner", identityPluginId: "identity.partner");

        var effective = EffectiveSurface.From([child, root]);

        Assert.Equal("identity.kunde", effective.IdentityPluginId);
        Assert.Equal(root.Id, effective.RootId);
    }

    [Fact]
    public void TwoNodesUnderOneRootShareTheirApplicationBoundary()
    {
        var root = Node("portal", "/portal");
        var a = Node("a", "a");
        var b = Node("b", "b");

        Assert.Equal(
            EffectiveSurface.From([a, root]).RootId,
            EffectiveSurface.From([b, root]).RootId);
    }
}
