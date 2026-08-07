using Callora.Core.Infrastructure.Http;
using Xunit;

namespace Callora.Core.Tests.Surface;

/// <summary>
/// Der Surface-Catch-All darf plattformeigene Pfade nicht beantworten.
/// </summary>
/// <remarks>
/// Er fängt <c>/{**surfacePath}</c> — also jeden unaufgelösten Pfad, auch <c>/api/…</c>, wenn
/// dort ein Endpunkt fehlt oder der Aufrufer sich vertippt. Ohne Abgrenzung kam darauf 200
/// mit einer gerenderten Seite und einem gesetzten Surface-Session-Cookie zurück.
///
/// <para>
/// Ein 200 mit falschem Inhalt ist die unangenehmste Sorte Fehler: kein Statuscode, kein
/// Log-Eintrag, nichts, was nach einem Problem aussieht. Der Aufrufer bekommt HTML, wo er
/// JSON erwartet, und meldet einen Parse-Fehler — gesucht wird dann überall, nur nicht beim
/// Routing. Genau so blieb ein falscher API-Pfad im Composer-Bundle unsichtbar, bis jemand
/// die Oberfläche öffnete.
/// </para>
/// </remarks>
public sealed class SurfaceCatchAllLeavesPlatformPathsAloneTests
{
    [Theory]
    [InlineData("/api")]
    [InlineData("/api/ext/admin/composer/pages")]
    [InlineData("/api/auth/login")]
    [InlineData("/workspace/public/navigation")]
    [InlineData("/plugin-assets/demo/app/admin/main.js")]
    [InlineData("/manifests/plugin-ui-assets.manifest.json")]
    [InlineData("/health")]
    [InlineData("/ready")]
    [InlineData("/swagger")]
    public void PlatformPathsAreRecognised(string path)
    {
        Assert.True(PlatformOwnedPathSegments.IsPlatformOwned(path), path);
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/portal")]
    [InlineData("/portal/kontakt")]
    // Ein Segment, das nur so ANFÄNGT wie ein plattformeigenes, gehört der Fläche:
    // /apiary ist keine API, und eine Surface mit diesem Pfad muss rendern.
    [InlineData("/apiary")]
    [InlineData("/workspaces-uebersicht")]
    public void SurfacePathsAreLeftAlone(string path)
    {
        Assert.False(PlatformOwnedPathSegments.IsPlatformOwned(path), path);
    }

    [Fact]
    public void AnEmptyPathIsNotPlatformOwned()
    {
        // Der Renderer setzt fehlende Pfade auf "/" — die Wurzel gehört der Fläche.
        Assert.False(PlatformOwnedPathSegments.IsPlatformOwned(null));
        Assert.False(PlatformOwnedPathSegments.IsPlatformOwned(""));
    }
}
