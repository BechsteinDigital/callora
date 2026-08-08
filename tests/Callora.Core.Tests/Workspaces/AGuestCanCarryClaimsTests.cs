using Callora.Core.Application.Surfaces;
using Callora.Core.Application.Workspaces;
using Callora.Core.Domain.Workspaces;
using Xunit;

namespace Callora.Core.Tests.Workspaces;

/// <summary>
/// Was ein Besucher auf einer Fläche mitbringt: seine eigenen Claims und die, die die Fläche
/// jedem gewährt.
/// </summary>
/// <remarks>
/// Vorher hatte ein nicht angemeldeter Aufrufer IMMER eine leere Menge — <c>ClaimsOf</c> gab für
/// alles außer <c>AuthenticatedSurfaceCaller</c> ein leeres Set zurück. Damit war jede Ansicht
/// mit einer Anforderung auf einer Fläche ohne Identitätsanbieter unerreichbar, auch für einen
/// Gast mit gültiger Einladung. Eine Videokonferenz konnte so nicht funktionieren, und nirgends
/// stand, warum.
/// </remarks>
public sealed class AGuestCanCarryClaimsTests
{
    [Fact]
    public void AGuestGetsWhatTheSurfaceGrants()
    {
        // Der Fall, für den es das Feld gibt: kein Login nötig, trotzdem soll die Konferenz
        // erreichbar sein.
        var claims = SurfaceVisibility.ClaimsOn(
            Guest(),
            "communication.calls,videoconference.join");

        Assert.True(SurfaceVisibility.Satisfies("communication.calls", claims));
        Assert.True(SurfaceVisibility.Satisfies("videoconference.join", claims));
    }

    [Fact]
    public void WithoutAGrantAGuestStillHasNothing()
    {
        // Gegenprobe: Die Gewährung ist eine Entscheidung, kein Standard. Ohne sie bleibt es
        // dabei, dass eine Anforderung eine Anforderung ist.
        var claims = SurfaceVisibility.ClaimsOn(Guest(), grantedClaims: null);

        Assert.Empty(claims);
        Assert.False(SurfaceVisibility.Satisfies("communication.calls", claims));
    }

    [Fact]
    public void AGrantAddsToWhatTheVisitorAlreadyHas()
    {
        // Die Gewährung ersetzt nichts. Wer angemeldet ist, behält seine eigenen Claims — sonst
        // nähme eine Fläche mit einer Gewährung ihren angemeldeten Besuchern etwas weg.
        var claims = SurfaceVisibility.ClaimsOn(
            new AuthenticatedSurfaceCaller(
                new SurfaceSubject("vc.example", "gast-1"),
                new SurfaceIdentity(
                    "Gast",
                    new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
                    {
                        ["videoconference.host"] = ["true"],
                    },
                    "ticket",
                    DateTimeOffset.UtcNow.AddMinutes(-1),
                    DateTimeOffset.UtcNow.AddHours(1))),
            "communication.calls");

        Assert.True(SurfaceVisibility.Satisfies("videoconference.host", claims));
        Assert.True(SurfaceVisibility.Satisfies("communication.calls", claims));
    }

    [Fact]
    public void AGrantIsCumulativeAlongTheChain()
    {
        // Wie die Anforderung: Was ein Elternteil gewährt, gilt auch für jede Unterseite. Alles
        // andere zwänge einen Betreiber, dieselbe Gewährung an jedem Knoten zu wiederholen — und
        // jede vergessene wäre eine Seite, die leer bleibt.
        var root = Surface("portal", "/", granted: "communication.calls");
        var child = Surface("kunden", "kunden", granted: "kunden.read", parent: root);

        var effective = EffectiveSurface.From([child, root]);
        var claims = SurfaceVisibility.ClaimsOn(Guest(), effective.GrantedClaims);

        Assert.True(SurfaceVisibility.Satisfies("communication.calls,kunden.read", claims));
    }

    private static SurfaceCaller Guest() =>
        new GuestSurfaceCaller(new SurfaceSubject(SurfaceIdentityIssuers.Guest, "g-1"));

    private static WorkspaceSurface Surface(
        string key,
        string prefix,
        string? granted = null,
        WorkspaceSurface? parent = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            SurfaceKey = key,
            PublicPathPrefix = prefix,
            GrantedClaims = granted,
            ParentSurfaceId = parent?.Id,
            IsActive = true,
            Workspace = new Callora.Core.Domain.Workspaces.Workspace { WorkspaceKey = "acme", IsActive = true },
        };
}
