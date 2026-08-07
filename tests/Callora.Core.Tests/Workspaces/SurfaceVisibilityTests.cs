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
}
