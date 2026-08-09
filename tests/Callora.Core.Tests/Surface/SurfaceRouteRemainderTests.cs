using Callora.Surface.Rendering.Api;
using Xunit;

namespace Callora.Core.Tests.Surface;

/// <summary>
/// Der Rest eines Anfragepfades hinter der Fläche, die ihn beansprucht hat.
/// </summary>
/// <remarks>
/// <c>/test/blub/gibtsnicht</c> antwortete mit 200 und dem Inhalt von <c>/test/blub</c>: Die
/// Auflösung nimmt das längste passende Präfix, und das Restsegment fiel unter den Tisch. Kein
/// Statuscode, kein Log — der Aufrufer sucht überall außer beim Routing.
/// </remarks>
public sealed class SurfaceRouteRemainderTests
{
    [Theory]
    // Genauer Treffer — nichts bleibt übrig.
    [InlineData("/test/blub", "/test/blub", "")]
    [InlineData("/test/blub", "/test/blub/", "")]
    [InlineData("/", "/", "")]
    // Was hinter der Fläche steht, ist ihr Rest.
    [InlineData("/test/blub", "/test/blub/gibtsnicht", "gibtsnicht")]
    [InlineData("/test/blub", "/test/blub/a/b", "a/b")]
    [InlineData("/", "/kunden/42", "kunden/42")]
    // Groß-/Kleinschreibung entscheidet nicht über die Zugehörigkeit.
    [InlineData("/test/blub", "/TEST/BLUB/x", "x")]
    public void TheRemainderIsWhatFollowsThePrefix(string prefix, string path, string expected) =>
        Assert.Equal(expected, SurfaceRouteRemainder.Of(prefix, path));

    [Fact]
    public void APathThatDoesNotBelongToTheSurfaceIsAllRemainder()
    {
        // Leer hieße „passt genau" — und der Aufrufer renderte still eine fremde Seite. Ein
        // Präfix, das nicht passt, muss als Ganzes auffallen.
        Assert.Equal("anderswo/tief", SurfaceRouteRemainder.Of("/test/blub", "/anderswo/tief"));
    }

    [Fact]
    public void ASegmentThatMerelyStartsTheSameIsNotAMatch()
    {
        // `/test/blubber` gehört nicht zu `/test/blub`. Ein reiner Zeichenvergleich sagte
        // „Rest: ber" und lieferte die falsche Seite aus.
        Assert.Equal("test/blubber", SurfaceRouteRemainder.Of("/test/blub", "/test/blubber"));
    }
}
