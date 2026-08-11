using Callora.Core.Application.Surfaces;
using Callora.Core.Application.Workspaces;
using Callora.Core.Domain.Workspaces;
using Xunit;

namespace Callora.Core.Tests.Workspaces;

/// <summary>
/// Wer einen Surface-Knoten sehen darf (ADR-019 §4).
/// <para>
/// Die tragende Aussage ist die Kumulation: Was ein Elternteil verlangt, gilt auch für seine
/// Nachfahren. Fiele sie weg, ließe sich der Schutz durch Tieferklicken umgehen — eine
/// Unterseite hat eine eigene URL.
/// </para>
/// </summary>
public sealed class SurfaceVisibilityTests
{
    private static WorkspaceSurface Node(string key, string? requiredClaims = null) =>
        new() { Id = Guid.NewGuid(), SurfaceKey = key, RequiredClaims = requiredClaims };

    private static SurfaceCaller Guest() =>
        new GuestSurfaceCaller(new SurfaceSubject("callora.surface-guest", "anon"));

    private static SurfaceCaller WithClaims(params string[] claims) =>
        new AuthenticatedSurfaceCaller(
            new SurfaceSubject("issuer", "subject"),
            new SurfaceIdentity(
                "Name",
                claims.ToDictionary(claim => claim, _ => (IReadOnlyList<string>)["true"], StringComparer.Ordinal),
                "test",
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow.AddHours(1)));

    [Fact]
    public void AnodeWithoutRequirementsIsVisibleToEveryone()
    {
        Assert.True(SurfaceVisibility.IsVisibleTo([Node("portal")], Guest()));
    }

    [Fact]
    public void ARequirementHidesTheNodeFromAGuest()
    {
        // Ein anonymer Aufruf darf nie eine Anforderung erfüllen, die jemand ausdrücklich
        // gestellt hat.
        Assert.False(SurfaceVisibility.IsVisibleTo([Node("partner", "partner")], Guest()));
    }

    [Fact]
    public void TheClaimUnlocksTheNode()
    {
        Assert.True(SurfaceVisibility.IsVisibleTo([Node("partner", "partner")], WithClaims("partner")));
    }

    [Fact]
    public void RequirementsAreCumulativeAlongTheChain()
    {
        // DIE Aussage. Ohne sie wäre `/portal/partner/downloads` ohne den Claim erreichbar,
        // obwohl `/portal/partner` ihn verlangt — und die Unterseite hat eine eigene URL, die
        // sich direkt aufrufen lässt.
        var chain = new[] { Node("downloads"), Node("partner", "partner"), Node("portal") };

        Assert.False(SurfaceVisibility.IsVisibleTo(chain, Guest()));
        Assert.True(SurfaceVisibility.IsVisibleTo(chain, WithClaims("partner")));
    }

    [Fact]
    public void EveryRequiredClaimMustBePresent()
    {
        // UND, nicht ODER: Wer zwei Claims fordert, meint beide. Ein ODER machte aus zwei
        // Bedingungen versehentlich eine.
        var chain = new[] { Node("intern", "partner,mitarbeiter") };

        Assert.False(SurfaceVisibility.IsVisibleTo(chain, WithClaims("partner")));
        Assert.True(SurfaceVisibility.IsVisibleTo(chain, WithClaims("partner", "mitarbeiter")));
    }

    [Fact]
    public void RequirementsAddUpAcrossLevels()
    {
        var chain = new[] { Node("tief", "b"), Node("mitte", "a"), Node("portal") };

        Assert.False(SurfaceVisibility.IsVisibleTo(chain, WithClaims("a")));
        Assert.False(SurfaceVisibility.IsVisibleTo(chain, WithClaims("b")));
        Assert.True(SurfaceVisibility.IsVisibleTo(chain, WithClaims("a", "b")));
    }

    [Fact]
    public void ParseIgnoresWhitespaceAndDuplicates()
    {
        Assert.Equal(["a", "b"], SurfaceVisibility.Parse(" a , b ,a "));
        Assert.Empty(SurfaceVisibility.Parse(null));
        Assert.Empty(SurfaceVisibility.Parse("  "));
    }

    /// <summary>
    /// Der Befund hinter <c>IsReachableBy</c>: Am Kontext-Socket gibt es keinen aufgelösten
    /// Aufrufer, wenn die Cookie-Sitzung zu einer ANDEREN Fläche gehört. Genau dieser Fall — ein
    /// Aufrufer, der auf dem Renderpfad ein 404 bekäme — hatte dort ein Abo erhalten.
    /// </summary>
    [Fact]
    public void AnUnresolvedCallerCannotReachAGatedNode()
    {
        Assert.False(SurfaceVisibility.IsReachableBy("partner", null, caller: null, identityAvailable: true));
    }

    [Fact]
    public void AnUnresolvedCallerReachesWhatTheSurfaceGrantsToEveryone()
    {
        // Dieselbe Regel wie auf dem Renderpfad: Was die Fläche jedem gewährt, gilt auch für den,
        // der keine eigene Identität mitbringt. Sonst wäre eine Anforderung auf einer Fläche ohne
        // Identitätsanbieter für niemanden erfüllbar.
        Assert.True(SurfaceVisibility.IsReachableBy("partner", "partner", caller: null, identityAvailable: true));
        Assert.True(SurfaceVisibility.IsReachableBy(null, null, caller: null, identityAvailable: true));
    }

    [Fact]
    public void TheCallersOwnClaimReachesTheNode()
    {
        Assert.True(SurfaceVisibility.IsReachableBy("partner", null, WithClaims("partner"), identityAvailable: true));
        Assert.False(SurfaceVisibility.IsReachableBy("partner", null, WithClaims("kunde"), identityAvailable: true));
    }

    [Fact]
    public void WithoutAnIdentitySubsystemAGatedNodeIsUnreachableEvenIfTheSurfaceGrantsTheClaim()
    {
        // Ohne Identitäts-Subsystem gibt es keine Claims — auch keine gewährten. Ein Knoten mit
        // Anforderung durchzulassen wäre die gefährlichste Variante: Die Anforderung stünde in
        // der Verwaltung und wirkte nicht.
        Assert.False(SurfaceVisibility.IsReachableBy("partner", "partner", caller: null, identityAvailable: false));
        Assert.True(SurfaceVisibility.IsReachableBy(null, null, caller: null, identityAvailable: false));
    }
}
