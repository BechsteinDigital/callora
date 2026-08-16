using Callora.Core.Extensibility;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;

namespace Callora.Core.Infrastructure.Plugins;

/// <summary>
/// Liefert veröffentlichte Plugin-Assets unter <c>/plugin-assets</c> aus — aus genau dem
/// Verzeichnis, in das <see cref="PluginUiAssetPublisher"/> schreibt.
/// </summary>
/// <remarks>
/// Ein zusätzliches <c>UseStaticFiles</c> und nicht der voreingestellte Aufruf: Der hängt an
/// <see cref="IWebHostEnvironment.WebRootPath"/>, und der ist LEER, wenn das Projekt kein
/// <c>wwwroot/</c>-Verzeichnis hat. Für ein Kompositionsprojekt, das seine statischen Dateien
/// aus Paketen bezieht, ist das der Normalfall.
///
/// <para>
/// Vorher wich nur der Publisher auf <c>AppContext.BaseDirectory/wwwroot</c> aus; die
/// Auslieferung kannte diesen Rückfall nicht. Jedes Plugin-Bundle lief in einen 404, die
/// Admin-Shell meldete für jedes Plugin einen Ladefehler, und das sah nach einem Fehler im
/// Plugin aus. Als eigene Methode, weil ein Test sie sonst nicht aufrufen kann, ohne die
/// gesamte Host-Komposition zu bauen — und ein Test, den niemand schreibt, ist der Grund,
/// warum zwei Rückfälle auseinanderlaufen.
/// </para>
/// </remarks>
[CalloraInternal("Auslieferung veröffentlichter Plugin-Assets — kein Plugin-Vertrag")]
public static class PluginAssetStaticFiles
{
    /// <summary>Hängt die Auslieferung in die Pipeline. Legt das Verzeichnis an, falls es fehlt.</summary>
    public static void Use(IApplicationBuilder app, IWebHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(environment);

        var root = Path.Combine(PluginAssetWebRoot.Resolve(environment), "plugin-assets");

        // PhysicalFileProvider verlangt ein existierendes Verzeichnis; der Publisher legt es
        // erst später an.
        Directory.CreateDirectory(root);

        app.UseStaticFiles(new StaticFileOptions
        {
            RequestPath = "/plugin-assets",
            FileProvider = new PhysicalFileProvider(root),
            OnPrepareResponse = PluginAssetCaching.Apply,

            // Ohne dies liefert die Auslieferung NUR bekannte Endungen aus. Alles andere fällt
            // durch — und landet bei der nächsten Middleware, die die Adresse für eine Fläche hält
            // und mit 401 antwortet. Ein Plugin, das ein Modell, eine Schriftart oder eine
            // Datentabelle mitliefert, bekommt also für eine Datei, die es selbst veröffentlicht
            // hat, „nicht angemeldet" zurück. Erstes Opfer: ein `.tflite`-Segmentierungsmodell,
            // dessen Ausbleiben der Hintergrund-Weichzeichner als „nicht verfügbar" meldete — ohne
            // 404, ohne Logzeile, ohne irgendeinen Hinweis auf die Endung.
            //
            // `application/octet-stream` und nicht etwa geraten: Ein unbekannter Typ wird vom
            // Browser heruntergeladen statt ausgeführt, womit die Endung keine Ausführungsfrage
            // mehr ist. Die Dateien stammen ohnehin aus einem signierten Bundle.
            ServeUnknownFileTypes = true,
            DefaultContentType = "application/octet-stream",
        });

        // Was hier noch ankommt, gibt es unter diesem Präfix nicht — die Auslieferung darüber hätte
        // es sonst geliefert. Ohne diesen Abschluss läuft die Anfrage weiter in UseAuthentication
        // und bekommt 401 mit leerem Body.
        //
        // Das ist keine Feinheit, sondern der Unterschied zwischen einer Diagnose in einer Minute
        // und einer in einer Stunde: Ein leerer Body hat keinen Content-Type, und der Browser meldet
        // dann „Refused to execute script … its MIME type ('') is not executable". Genau danach hat
        // jemand die Auslieferungsoptionen, die CSP und die Content-Type-Zuordnung durchsucht — und
        // die Ursache war eine Datei, die das Bundle nie mitgebracht hatte (#306). Ein 404 hätte das
        // sofort gesagt.
        //
        // Der Pfad ist reserviert; eine Fläche kann dort nicht liegen. Es gibt also niemanden, an
        // den weiterzureichen wäre.
        app.Map("/plugin-assets", missing => missing.Run(context =>
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return Task.CompletedTask;
        }));
    }
}
