namespace Callora.Core.Application.Security;

/// <summary>
/// Eine Rolle, die die Installation eines Plugins anlegen soll.
/// </summary>
/// <remarks>
/// <para>
/// Der Betreiber soll nach der Installation eine brauchbare Rolle vorfinden statt einer Liste von
/// Schlüsseln, aus der er sie sich zusammenklickt. Die Schlüssel gab es schon — vergeben werden mussten
/// sie einzeln und von Hand, und wer das übersieht, hat ein installiertes Plugin, dessen Oberfläche für
/// jeden außer dem Super-Admin leer bleibt.
/// </para>
/// <para>
/// <see cref="Slug"/> und <see cref="PluginId"/> zusammen sind die Identität, nicht der Name. Wer die
/// Rolle in der Oberfläche umbenennt, soll beim nächsten Start nicht eine zweite danebengestellt
/// bekommen.
/// </para>
/// </remarks>
/// <param name="PluginId">Das Plugin, dem die Rolle gehört.</param>
/// <param name="Slug">Welche Rolle des Plugins das ist — <c>admin</c> für die automatische.</param>
/// <param name="RoleName">Der Name, unter dem sie angelegt wird, wenn es sie noch nicht gibt.</param>
/// <param name="PermissionKeys">Die Berechtigungen, die sie beim Anlegen bekommt.</param>
public sealed record PluginRoleTemplate(
    string PluginId,
    string Slug,
    string RoleName,
    IReadOnlyList<string> PermissionKeys);
