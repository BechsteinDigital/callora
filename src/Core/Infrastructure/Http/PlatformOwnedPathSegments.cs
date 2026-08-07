using Callora.Core.Extensibility;

namespace Callora.Core.Infrastructure.Http;

/// <summary>
/// Die obersten Pfadsegmente, die der Plattform gehören. Ein Catch-All darf sie nicht
/// beantworten.
/// </summary>
/// <remarks>
/// Zwei Catch-Alls konkurrieren um unaufgelöste Pfade: der Workspace-Storefront
/// (<c>/{**path}</c>) und der Surface-Renderer (<c>/{**surfacePath}</c>). Der erste hatte
/// diese Prüfung, der zweite nicht — und weil der Renderer in einer colocated Komposition
/// gewinnt, beantwortete er JEDEN unbekannten Pfad mit einer gerenderten Seite und setzte
/// dabei ein Surface-Session-Cookie. Auch <c>/api/…</c>.
///
/// <para>
/// Das ist die unangenehmste Sorte Fehler: 200 mit falschem Inhalt. Kein Statuscode, kein
/// Log-Eintrag, nichts, was nach einem Problem aussieht — der Aufrufer bekommt HTML, wo er
/// JSON erwartet, und meldet einen Parse-Fehler. Genau so blieb ein falscher API-Pfad im
/// Composer-Bundle unsichtbar, bis jemand die Oberfläche öffnete.
/// </para>
///
/// <para>
/// Nicht zu verwechseln mit <see cref="ReservedHostRoutePrefixes"/>: Das prüft, was ein
/// PLUGIN als Routen-Template beanspruchen darf. Hier geht es um eingehende Pfade, die kein
/// Catch-All beantworten soll.
/// </para>
/// </remarks>
[CalloraInternal("Catch-All-Abgrenzung — kein Plugin-Vertrag")]
public static class PlatformOwnedPathSegments
{
    private static readonly string[] Segments =
    [
        "api",
        "swagger",
        "workspace",
        "health",
        "ready",
        "plugin-assets",
        "manifests",
        "_nuxt"
    ];

    /// <summary>
    /// Wahr, wenn der Pfad mit einem plattformeigenen Segment beginnt — als ganzes Segment,
    /// nicht als Präfix: <c>/api/x</c> ja, <c>/apiary</c> nein.
    /// </summary>
    public static bool IsPlatformOwned(string? requestPath)
    {
        if (string.IsNullOrWhiteSpace(requestPath))
        {
            return false;
        }

        var normalized = requestPath.TrimStart('/');
        foreach (var segment in Segments)
        {
            if (normalized.Equals(segment, StringComparison.OrdinalIgnoreCase) ||
                normalized.StartsWith(segment + "/", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
