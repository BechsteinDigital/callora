using Callora.Core.Application.Workspaces;
using Callora.Core.Domain.Workspaces;
using Xunit;
using WorkspaceEntity = Callora.Core.Domain.Workspaces.Workspace;

namespace Callora.Core.Tests.Workspaces;

/// <summary>
/// Eine Basis-URL kann einen Workspace bezeichnen oder eine Fläche. Eine Seite kann nie eine
/// Basis-URL sein — sie ist immer ein Pfadsegment unterhalb ihrer Fläche.
/// </summary>
/// <remarks>
/// Trägt weder die Fläche noch der Workspace einen Host, beginnt der Pfad mit dem
/// Workspace-Schlüssel. Ohne dieses Segment beanspruchte jeder frisch angelegte Workspace die
/// gesamte Origin: Zwei <c>default</c>-Flächen mit Präfix <c>/</c> waren nicht unterscheidbar,
/// die Auswertung entschied still und immer gleich, und der zweite Workspace war unerreichbar
/// — ohne Hinweis in der Administration, im Log oder sonstwo.
/// </remarks>
public sealed class PublicRouteCarriesTheWorkspaceTests
{
    [Fact]
    public void WithoutAnyHostThePathStartsWithTheWorkspace()
    {
        var root = Surface("acme", "default", prefix: "/");

        var effective = EffectiveSurface.From([root]);

        Assert.Null(effective.PublicHost);
        Assert.Equal("/acme", effective.PublicPathPrefix);
    }

    [Fact]
    public void AChildAppendsItsSegmentBelowTheWorkspace()
    {
        var root = Surface("acme", "default", prefix: "/");
        var child = Surface("acme", "partner", prefix: "partner", parent: root);

        var effective = EffectiveSurface.From([child, root]);

        Assert.Equal("/acme/partner", effective.PublicPathPrefix);
    }

    [Fact]
    public void AWorkspaceHostReplacesTheWorkspaceSegment()
    {
        // kunde.de IST der Workspace — ihn zusätzlich in den Pfad zu schreiben wäre keine
        // Unterscheidung, sondern eine Wiederholung.
        var root = Surface("acme", "default", prefix: "/", workspaceHost: "kunde.de");
        var child = Surface("acme", "partner", prefix: "partner", parent: root, workspaceHost: "kunde.de");

        var effective = EffectiveSurface.From([child, root]);

        Assert.Equal("kunde.de", effective.PublicHost);
        Assert.Equal("/partner", effective.PublicPathPrefix);
    }

    [Fact]
    public void ASurfaceHostWinsOverTheWorkspaceHost()
    {
        // Die Fläche ist das speziellere Signal: Wer portal.kunde.de auf eine Fläche legt,
        // meint diese Fläche, auch wenn der Workspace kunde.de trägt.
        var root = Surface("acme", "portal", prefix: "/", host: "portal.kunde.de", workspaceHost: "kunde.de");

        var effective = EffectiveSurface.From([root]);

        Assert.Equal("portal.kunde.de", effective.PublicHost);
        Assert.Equal("/", effective.PublicPathPrefix);
    }

    [Fact]
    public void TwoFreshWorkspacesNoLongerClaimTheSameRoute()
    {
        // Der Fall, der das Modell nötig gemacht hat: Beide legen eine default-Fläche mit
        // Präfix "/" an, beide ohne Host.
        var first = EffectiveSurface.From([Surface("acme", "default", prefix: "/")]);
        var second = EffectiveSurface.From([Surface("globex", "default", prefix: "/")]);

        Assert.NotEqual(first.PublicPathPrefix, second.PublicPathPrefix);
    }

    private static WorkspaceSurface Surface(
        string workspaceKey,
        string surfaceKey,
        string prefix,
        string? host = null,
        string? workspaceHost = null,
        WorkspaceSurface? parent = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            SurfaceKey = surfaceKey,
            PublicPathPrefix = prefix,
            PublicHost = host,
            ParentSurfaceId = parent?.Id,
            IsActive = true,
            Workspace = new WorkspaceEntity
            {
                WorkspaceKey = workspaceKey,
                PublicHost = workspaceHost,
                IsActive = true,
            },
        };
}
