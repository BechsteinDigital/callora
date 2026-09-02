using Callora.Core.Domain.Security;

namespace Callora.Core.Domain.Workspaces;

/// <summary>
/// Eine Rolle, die jemand <em>in einem Workspace</em> trägt — zusätzlich zu seiner Mitgliedsrolle.
/// </summary>
/// <remarks>
/// <para>
/// <b>Warum es das braucht.</b> Eine Mitgliedschaft kannte genau zwei Antworten, fest im Code:
/// Administrator oder Mitglied. „Darf die Telefonanlage benutzen, aber nichts ändern" ließ sich damit
/// nicht sagen — es gab keinen Ort, an dem es hätte stehen können.
/// </para>
/// <para>
/// <b>Dieselben Rollen wie global, nicht ein zweites Rollensystem.</b> <see cref="BackendRbacRole"/>
/// hat bereits Berechtigungen, eine Verwaltungsoberfläche und eine API; ein zweiter Satz Tabellen
/// daneben wäre eine zweite Antwort auf „was darf diese Rolle" und liefe an dem Tag auseinander, an
/// dem jemand nur eine davon anfasst. Was eine Rolle in einem Workspace tatsächlich bewirkt, entsteht
/// bei der Anmeldung: Ihre Schlüssel werden auf das gefiltert, was in diesem Workspace überhaupt
/// gelten darf.
/// </para>
/// <para>
/// <b>Mehrere je Mitgliedschaft.</b> Genau deshalb ist es eine eigene Zeile und kein Feld: „PBX lesen"
/// und „Medien verwalten" sind zwei Entscheidungen, und eine Person kann beide brauchen. Die globale
/// Zuweisung bleibt bei einer Rolle — sie entscheidet, ob jemand Plattform-Operator ist, und das ist
/// keine Frage, auf die es mehrere Antworten geben kann.
/// </para>
/// </remarks>
public sealed class WorkspaceMembershipRole
{
    public Guid Id { get; set; }

    public Guid MembershipId { get; set; }

    public WorkspaceMembership Membership { get; set; } = null!;

    public Guid RoleId { get; set; }

    public BackendRbacRole Role { get; set; } = null!;

    public DateTimeOffset AssignedAtUtc { get; set; }
}
