namespace Callora.Core.Application.Lifecycle;

/// <param name="State">Der GEWÜNSCHTE Zustand, wie er in der Datenbank steht.</param>
/// <param name="IsRunning">
/// Ob das Plugin zur Laufzeit tatsächlich aktiv ist.
/// </param>
/// <remarks>
/// Zwei Angaben, weil es zwei Wahrheiten gibt: was gelten soll und was gilt. Ein Plugin, dessen
/// Aktivierung beim Start scheitert — eine fehlende Fähigkeit, ein Signaturproblem, eine
/// Ausnahme im Start —, bleibt in der Datenbank <c>Active</c>. Die Übersicht zeigte es dann als
/// „Aktiv", während es nichts tat; der Fehlschlag stand ausschließlich in einer Logzeile beim
/// Start, die niemand liest.
///
/// <para>
/// Abgeleitet statt gespeichert: Der Laufzeitzustand gehört der Laufzeit. Ihn in die Datenbank zu
/// schreiben hieße, ihn dort aktuell halten zu müssen — und ein Prozess, der abstürzt, hinterließe
/// eine Zeile, die behauptet, etwas laufe noch.
/// </para>
/// </remarks>
public sealed record PluginInstallationSnapshot(
    string PluginId,
    string DisplayName,
    string AssemblyPath,
    string? EntryTypeName,
    int State,
    DateTimeOffset InstalledAtUtc,
    DateTimeOffset UpdatedAtUtc,
    bool IsRunning = false);
