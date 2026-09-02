namespace Callora.Administration.Api;

/// <summary>Setzt die Rollen einer Mitgliedschaft.</summary>
/// <param name="Roles">
/// Der Stand danach, nicht eine Ergänzung. Eine Oberfläche zeigt Kästchen, und was der Betreiber
/// gesehen hat, ist der Zustand, den er meint — aus zwei Befehlen müsste er sich die Reihenfolge
/// merken, in der er sie angeklickt hat. Eine leere Liste nimmt alle weg.
/// </param>
public sealed record SetWorkspaceMemberRolesApiRequest(IReadOnlyList<string>? Roles);
