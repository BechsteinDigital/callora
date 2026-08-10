using Callora.Core.Application.Surfaces;
using Xunit;

namespace Callora.Core.Tests.Surface;

/// <summary>
/// Die Reichweite einer aus dem Cookie gelesenen Flächen-Sitzung: Sie gilt für genau die
/// Fläche, für die sie ausgestellt wurde — nicht für die Nachbarfläche und nicht für einen
/// anderen Workspace.
/// </summary>
public sealed class SurfaceSessionScopeTests
{
    [Fact]
    public void TheSameSurface_Matches()
    {
        Assert.True(SurfaceSessionScope.Matches(Context("acme", "desk"), "acme", "desk"));
    }

    [Fact]
    public void AnotherWorkspace_DoesNotMatch()
    {
        Assert.False(SurfaceSessionScope.Matches(Context("acme", "desk"), "globex", "desk"));
    }

    [Fact]
    public void AnotherSurfaceOfTheSameWorkspace_DoesNotMatch()
    {
        // Der Workspace allein genügt nicht: Ein öffentliches Portal und ein Agenten-Desktop
        // teilen den Datenbestand und haben doch verschiedene Besucher (ADR-019).
        Assert.False(SurfaceSessionScope.Matches(Context("acme", "portal"), "acme", "desk"));
    }

    [Theory]
    [InlineData("ACME", "DESK")]
    [InlineData("acme", "Desk")]
    public void KeysAreComparedCaseInsensitively(string workspaceKey, string surfaceKey)
    {
        Assert.True(SurfaceSessionScope.Matches(Context("acme", "desk"), workspaceKey, surfaceKey));
    }

    [Fact]
    public void NoSession_DoesNotMatch()
    {
        Assert.False(SurfaceSessionScope.Matches(null, "acme", "desk"));
    }

    [Theory]
    [InlineData(null, "desk")]
    [InlineData("acme", null)]
    [InlineData("", "desk")]
    [InlineData("acme", "  ")]
    public void AnIncompleteQuestion_IsAnsweredWithNo(string? workspaceKey, string? surfaceKey)
    {
        // Fail-closed: Wer nicht sagen kann, für welche Fläche er fragt, bekommt keine
        // Anmeldung zugesprochen.
        Assert.False(SurfaceSessionScope.Matches(Context("acme", "desk"), workspaceKey, surfaceKey));
    }

    private static SurfaceCallerContext Context(string workspaceKey, string surfaceKey) =>
        new(
            new GuestSurfaceCaller(new SurfaceSubject(SurfaceIdentityIssuers.Guest, "g-1")),
            "tenant-1",
            workspaceKey,
            surfaceKey);
}
