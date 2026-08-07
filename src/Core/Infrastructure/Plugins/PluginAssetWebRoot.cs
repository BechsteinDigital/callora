using Callora.Core.Extensibility;
using Microsoft.AspNetCore.Hosting;

namespace Callora.Core.Infrastructure.Plugins;

/// <summary>
/// Das Verzeichnis, unter dem veröffentlichte Plugin-Assets liegen — die eine Stelle, an
/// der Schreiber und Ausliefernder sich einigen.
/// </summary>
/// <remarks>
/// <see cref="IWebHostEnvironment.WebRootPath"/> ist leer, wenn das Projekt kein
/// <c>wwwroot/</c>-Verzeichnis hat. Für ein Kompositionsprojekt, das seine statischen
/// Dateien aus Paketen bezieht, ist genau das der Normalfall — <c>callora-production</c>
/// hat keines.
///
/// <para>
/// Der Publisher hatte dafür einen eigenen Rückfall auf
/// <c>AppContext.BaseDirectory/wwwroot</c> und schrieb dorthin; die Auslieferung kannte ihn
/// nicht und lieferte nichts aus. Ergebnis: Jedes Plugin-Bundle lief in einen 404, die
/// Admin-Shell meldete für jedes Plugin einen Ladefehler, und beides sah nach einem Fehler
/// im Plugin aus. Zwei Rückfälle, die sich nicht kennen, sind schlimmer als keiner.
/// </para>
/// </remarks>
[CalloraInternal("Pfadauflösung für veröffentlichte Plugin-Assets — kein Plugin-Vertrag")]
public static class PluginAssetWebRoot
{
    /// <summary>
    /// Der Web-Root, unter dem <c>plugin-assets/</c> entsteht und gefunden wird.
    /// </summary>
    public static string Resolve(IWebHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);

        return string.IsNullOrWhiteSpace(environment.WebRootPath)
            ? Path.Combine(AppContext.BaseDirectory, "wwwroot")
            : environment.WebRootPath;
    }
}
