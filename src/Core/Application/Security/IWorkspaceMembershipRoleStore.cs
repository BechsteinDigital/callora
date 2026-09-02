using Callora.Core.Extensibility;

namespace Callora.Core.Application.Security;

/// <summary>
/// Welche Rollen jemand in einem Workspace trägt.
/// </summary>
/// <remarks>
/// Nur Namen, keine Berechtigungen: Was eine Rolle enthält, weiß <see cref="IBackendRbacStore"/>, und
/// zweimal dieselbe Frage zu beantworten ist der Anfang zweier Antworten.
/// </remarks>
[CalloraInternal("Rollenzuweisung je Mitgliedschaft — Durchsetzung, kein Plugin-Vertrag (REV2 §7.2)")]
public interface IWorkspaceMembershipRoleStore
{
    /// <summary>Die Rollennamen dieser Mitgliedschaft, sortiert.</summary>
    Task<IReadOnlyList<string>> ListRolesAsync(
        string workspaceKey, string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ersetzt die Zuweisungen und antwortet mit dem, was danach gilt.
    /// </summary>
    /// <remarks>
    /// Ersetzen statt Hinzufügen/Entfernen: Eine Oberfläche zeigt Kästchen, und der Zustand danach ist
    /// das, was der Betreiber gesehen hat. Zwei Befehle daraus zu machen hieße, sich die Reihenfolge
    /// merken zu müssen, in der er sie angeklickt hat.
    /// </remarks>
    /// <returns>
    /// Die gültigen Rollennamen, oder <see langword="null"/>, wenn es diese Mitgliedschaft nicht gibt.
    /// </returns>
    Task<IReadOnlyList<string>?> ReplaceRolesAsync(
        string workspaceKey,
        string userId,
        IReadOnlyCollection<string> roles,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Die externen Benutzerkennungen, die diese Rolle über eine Mitgliedschaft tragen.
    /// </summary>
    /// <remarks>
    /// Für den Widerruf von Sitzungen. Ohne das behielte jemand, dem eine Rolle über eine
    /// Mitgliedschaft entzogen wurde, ihre Berechtigungen bis zum Ablauf seines Tokens — die
    /// Berechtigungen stehen darin, nicht in der Datenbank.
    /// </remarks>
    Task<IReadOnlyList<string>> ListUsersWithRoleAsync(
        string role, CancellationToken cancellationToken = default);
}
