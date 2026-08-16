namespace Callora.Core.Application.Plugins;

/// <summary>
/// Übersetzt zwischen dem Dateipfad einer Plugin-Assembly und der Form, in der er gespeichert
/// wird.
/// </summary>
/// <remarks>
/// Ein absoluter Pfad in der Datenbank bindet den Datenbestand an die Umgebung, in der zufällig
/// installiert wurde: Wer einmal per <c>dotnet run</c> installiert und den Host danach im
/// Container startet, hat Zeilen, die auf <c>/home/…</c> zeigen, während dieselben Dateien unter
/// <c>/app/…</c> liegen. Die Plugins laden dann nicht, die Verwaltung zeigt sie trotzdem als
/// installiert, und repariert wurde das bisher mit einem <c>UPDATE … replace(…)</c> von Hand
/// (#307).
///
/// Ein Pfad unter einer der beiden konfigurierten Wurzeln wird deshalb relativ zu ihr gespeichert
/// und beim Laden wieder aufgelöst — ein Umgebungswechsel ändert dann die Wurzel, nicht den
/// Datenbestand. Plugins außerhalb beider Wurzeln (per NuGet, ein Operator-Pfad) haben keine
/// solche Bezugsgröße und behalten ihren absoluten Pfad; genau diese Unterscheidung soll sichtbar
/// sein statt implizit zu bleiben.
/// </remarks>
public interface IPluginAssemblyPathPortability
{
    /// <summary>
    /// Die zu speichernde Form eines Dateipfads: relativ zur Wurzel, unter der er liegt,
    /// andernfalls unverändert.
    /// </summary>
    string ToStoredPath(string fileSystemPath);

    /// <summary>
    /// Der Dateipfad zu einer gespeicherten Form: gegen die aktuell konfigurierte Wurzel
    /// aufgelöst, andernfalls unverändert.
    /// </summary>
    string ToFileSystemPath(string storedPath);

    /// <summary>
    /// Ob ein gespeicherter Pfad aus einer der Plugin-Wurzeln stammt — für beide Formen, weil
    /// Bestand aus der Zeit vor dem Umbau dort absolut steht.
    /// </summary>
    bool IsUnderPluginRoots(string storedPath);

    /// <summary>
    /// Sucht dieselbe Datei unter den aktuell konfigurierten Wurzeln, wenn der gespeicherte Pfad
    /// ins Leere zeigt — der Fall „installiert in der einen Umgebung, gestartet in der anderen".
    /// </summary>
    /// <remarks>
    /// Probiert die Endstücke des gespeicherten Pfads, längstes zuerst, und nimmt das erste, das
    /// unter einer Wurzel wirklich existiert. Geraten wird dabei nichts: Was zurückkommt, ist eine
    /// Datei, die da ist — genau der Abgleich, den ein Betreiber im Kopf macht, wenn er
    /// <c>/home/…</c> gegen <c>/app/…</c> tauscht.
    /// </remarks>
    bool TryLocateInRoots(string storedPath, out string fileSystemPath);
}
