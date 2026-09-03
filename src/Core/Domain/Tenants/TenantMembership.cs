using Callora.Core.Domain.Security;

namespace Callora.Core.Domain.Tenants;

/// <summary>
/// Wer einen Mandanten verwaltet — die Ebene zwischen Plattformbetreiber und Workspace.
/// </summary>
/// <remarks>
/// <para>
/// <b>Warum es das braucht.</b> ADR-014 §18 nennt einen <c>TenantAdmin</c>, der die Workspaces eines
/// Kunden verwaltet, während Workspace-Admins je Workspace arbeiten. Im Code gab es diese Ebene nicht:
/// <see cref="Callora.Core.Application.Security.BackendAuthScopes"/> kannte nur Plattform und
/// Workspace. Damit war „die Agentur betreibt die Instanz, ihre Kunden verwalten ihre eigenen
/// Workspaces" nicht ausdrückbar — entweder jemand war Betreiber und sah alle Mandanten, oder er saß
/// in genau einem Workspace fest.
/// </para>
/// <para>
/// <b>Ein eigenes Aggregat, kein nullbarer Workspace an <see cref="Callora.Core.Domain.Workspaces.WorkspaceMembership"/>.</b>
/// Ein nullbarer <c>WorkspaceId</c> hätte jede bestehende Abfrage „ist Mitglied dieses Workspace"
/// still mehrdeutig gemacht: Zeilen ohne Workspace hätten überall mitgezählt, wo niemand mit ihnen
/// rechnet. Das ist dieselbe Fehlerklasse, die der Kommentar in
/// <c>BackendClaimsTransformation</c> festhält — ein Namensraum, der zwei Dinge bedeutet, und
/// niemand sieht es der Abfrage an.
/// </para>
/// <para>
/// <b>Zwei Antworten, nicht beliebig viele.</b> Anders als im Workspace gibt es hier bewusst keine
/// zusätzlich zuweisbaren Rollen: Auf Mandantenebene lauten die Fragen „verwaltet den Mandanten" oder
/// „sieht ihn", und ein zweiter Satz Tabellen für eine Unterscheidung, die noch niemand gebraucht hat,
/// wäre Vorrat. Der Erweiterungspunkt ist derselbe wie im Workspace, falls er gebraucht wird.
/// </para>
/// </remarks>
public sealed class TenantMembership
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Tenant Tenant { get; set; } = null!;

    public Guid UserId { get; set; }

    public BackendUser User { get; set; } = null!;

    /// <summary>
    /// Die Mitgliedsrolle: <c>admin</c> oder alles andere. Sie entscheidet, was eine mandantenweite
    /// Sitzung tragen darf — siehe <c>TenantRolePermissions</c>.
    /// </summary>
    public string Role { get; set; } = string.Empty;

    public DateTimeOffset AssignedAtUtc { get; set; }
}
