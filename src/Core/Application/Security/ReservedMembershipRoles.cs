using Callora.Core.Application.Policies;
using Callora.Core.Extensibility;

namespace Callora.Core.Application.Security;

/// <summary>
/// Rollennamen, die eine Workspace-Mitgliedschaft nicht tragen darf, weil sie an anderer
/// Stelle Plattform-Autorität bedeuten.
/// </summary>
/// <remarks>
/// <para>
/// Die Mitgliedsrolle ist ein freier String: Wer <c>membership.update</c> hat — und das hat
/// jeder Workspace-Admin (<see cref="WorkspaceRolePermissions"/>) — schreibt sie selbst.
/// Sie wird beim Anmelden zum Rollen-Claim, und
/// <c>EndpointAuthorizationExtensions.HasPermission</c> beantwortet
/// <c>IsInRole(<see cref="BackendRoles.SuperAdmin"/>)</c> mit einem bedingungslosen Ja.
/// Ohne diese Sperre wurde aus „Admin in EINEM Workspace" durch einen selbst geschriebenen
/// Rollennamen „Operator über ALLE" — die Grenze, auf der das ganze Mandantenmodell steht.
/// </para>
/// <para>
/// Geprüft wird an zwei Stellen, mit Absicht doppelt: beim Schreiben, damit die Zeile gar
/// nicht erst entsteht, und beim Anmelden, weil ein bereits vergifteter Datenbestand sonst
/// wirksam bliebe. Eine Sperre, die nur den Schreibpfad kennt, repariert die Vergangenheit
/// nicht.
/// </para>
/// </remarks>
[CalloraInternal("Rollen-Grenze der Mitgliedschaft — Durchsetzung, kein Plugin-Vertrag")]
public static class ReservedMembershipRoles
{
    /// <summary>
    /// Ob <paramref name="role"/> für eine Workspace-Mitgliedschaft verboten ist.
    /// </summary>
    /// <param name="role">Der gewünschte Rollenname; <c>null</c>/leer ist nicht reserviert.</param>
    /// <param name="options">Host-Optionen, aus denen die Operator-Rollen stammen.</param>
    public static bool IsReserved(string? role, BackendHostOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(role))
        {
            return false;
        }

        var candidate = role.Trim();

        // Fest verdrahtet, nicht nur aus der Konfiguration: Auf diese beiden Namen prüft der
        // Code selbst (EndpointAuthorizationExtensions, WorkspaceScopeEvaluator). Stünde hier
        // nur PlatformOperatorRoles, öffnete eine geänderte Konfiguration die Lücke wieder.
        if (string.Equals(candidate, BackendRoles.SuperAdmin, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(candidate, BackendRoles.HostApi, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        foreach (var operatorRole in options.PlatformOperatorRoles ?? [])
        {
            if (string.Equals(operatorRole?.Trim(), candidate, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
