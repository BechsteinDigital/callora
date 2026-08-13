using Callora.Core.Domain.Workspaces;

namespace Callora.Core.Application.Workspaces;

/// <summary>
/// Die Menge aller Flächen, gegen die eine öffentliche Adresse aufgelöst wird — als Ganzes
/// geladen, nicht je Anfrage abgefragt.
/// </summary>
/// <remarks>
/// <para>
/// Der Zuschnitt folgt Shopwares <c>CachedDomainLoader</c>, und zwar in dem Punkt, der zählt: Der
/// angefragte HOST ist kein Schlüssel. Er kommt aus dem Request, ist also von außen bestimmbar —
/// wer ihn zum Cache-Schlüssel macht, lädt jeden erfundenen <c>Host:</c>-Header als eigenen
/// Eintrag ein. Geladen wird stattdessen die ganze Tabelle unter einem festen Schlüssel; das
/// Zuordnen von Host und Pfad passiert danach im Speicher und ist billig.
/// </para>
/// <para>
/// Der Pfad kommt aus demselben Grund nicht in Frage, nur noch deutlicher: Er variiert mit jeder
/// Unterseite.
/// </para>
/// <para>
/// Ungültig wird die Menge nicht durch Zeitablauf, sondern durch Schreiben — deshalb
/// <see cref="Invalidate"/>. Ein Cache über die Routenauflösung hält auch eine
/// Sicherheitsentscheidung fest: Eine abgeschaltete Elternfläche nimmt ihre Kinder mit vom Netz,
/// und ein Eintrag, der das überlebt, liefert abgeschaltete Seiten weiter aus. Die Invalidierung
/// ist hier keine Kür, sondern die Bedingung dafür, dass der Cache überhaupt zulässig ist.
/// </para>
/// </remarks>
public interface ISurfaceRouteTable
{
    /// <summary>
    /// Alle Flächen samt Workspace und Mandant. Die Rückgabe ist zum Lesen bestimmt: Sie wird
    /// zwischen Anfragen geteilt, und wer ein Element verändert, verändert es für alle.
    /// </summary>
    ValueTask<IReadOnlyList<WorkspaceSurface>> LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Verwirft die geladene Menge. Von jedem Schreibvorgang zu rufen, der Adressierung,
    /// Aktivierung, Vererbung oder Theme-Zuordnung einer Fläche berührt.
    /// </summary>
    void Invalidate();
}
