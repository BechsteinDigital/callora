namespace Callora.Core.Application.Security;

/// <summary>
/// Je Plugin eine Rolle mit allen seinen Berechtigungen.
/// </summary>
/// <remarks>
/// <para>
/// <b>Woher die Schlüssel kommen, steht nicht mehr hier</b>, sondern in
/// <see cref="IPluginPermissionMap"/> — beide Zulieferwege, ein Ergebnis. Herausgelöst, als die Sitzung
/// eines Workspace-Admins dieselbe Zuordnung brauchte: Zwei Fassungen davon wären zwei Antworten auf
/// dieselbe Frage, und sie würden an dem Tag auseinanderlaufen, an dem jemand nur eine anfasst.
/// </para>
/// <para>
/// <b>Eine Rolle, nicht drei.</b> „Ansicht", „Verwaltung", „Nur Ansagen" sind Zuschnitte, die das
/// Plugin selbst kennen muss; hier ist nur bekannt, welche Schlüssel es überhaupt gibt. Eine geratene
/// Aufteilung wäre schlimmer als keine: Sie sieht aus, als hätte jemand nachgedacht, und der Betreiber
/// prüft sie deshalb nicht nach. Feinere Rollen kommen aus dem Manifest, sobald es sie erlaubt — bis
/// dahin leitet man sie von dieser ab, was ein Klick ist.
/// </para>
/// </remarks>
public sealed class PluginAdminRoleTemplates(IPluginPermissionMap permissions) : IPluginRoleTemplateSource
{
    /// <summary>Der Slug der automatisch angelegten Rolle.</summary>
    public const string AdminSlug = "admin";

    private readonly IPluginPermissionMap _permissions =
        permissions ?? throw new ArgumentNullException(nameof(permissions));

    /// <inheritdoc />
    public async Task<IReadOnlyList<PluginRoleTemplate>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        var byPlugin = await _permissions.ByPluginAsync(cancellationToken).ConfigureAwait(false);

        return
        [
            .. byPlugin
                .Where(entry => entry.Value.Count > 0)
                .OrderBy(entry => entry.Key, StringComparer.Ordinal)
                .Select(entry => new PluginRoleTemplate(
                    entry.Key,
                    AdminSlug,
                    // Dieselbe Form wie die Rollen des Kerns ("superadmin", "host.api"): ein Bezeichner,
                    // kein Fließtext. Ein sprechender Name müsste übersetzt werden, und ein Rollenname
                    // ist Daten — er steht in Tokens, Logzeilen und Skripten und soll sich nicht mit der
                    // Sprache der Oberfläche ändern.
                    $"{entry.Key}.{AdminSlug}",
                    [.. entry.Value]))
        ];
    }
}
