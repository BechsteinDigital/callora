using Callora.Core.Application.Plugins;
using Callora.Core.Application.Plugins.Contracts;

namespace Callora.Core.Application.Security;

/// <summary>
/// Je Plugin eine Rolle mit allen seinen Berechtigungen.
/// </summary>
/// <remarks>
/// <para>
/// <b>Beide Zulieferwege, ein Ergebnis</b> — dieselbe Regel wie in
/// <see cref="BackendPermissionInventory"/>, und aus demselben Grund. Ein Plugin kann seine Schlüssel
/// im Manifest deklarieren oder über <see cref="IHostAdminApiExtensionContributor"/> beisteuern; heute
/// tut von vier installierten Plugins genau eines das erste. Nur das Manifest zu lesen hieße, für die
/// anderen drei keine Rolle anzulegen — und zwar wortlos.
/// </para>
/// <para>
/// <b>Eine Rolle, nicht drei.</b> „Ansicht", „Verwaltung", „Nur Ansagen" sind Zuschnitte, die das
/// Plugin selbst kennen muss; hier ist nur bekannt, welche Schlüssel es überhaupt gibt. Eine geratene
/// Aufteilung wäre schlimmer als keine: Sie sieht aus, als hätte jemand nachgedacht, und der Betreiber
/// prüft sie deshalb nicht nach. Feinere Rollen kommen aus dem Manifest, sobald es sie erlaubt — bis
/// dahin leitet man sie von dieser ab, was ein Klick ist.
/// </para>
/// </remarks>
public sealed class PluginAdminRoleTemplates(
    ICalloraPluginCatalog pluginCatalog,
    IPluginDeclaredPermissionCatalog declaredPermissions) : IPluginRoleTemplateSource
{
    /// <summary>Der Slug der automatisch angelegten Rolle.</summary>
    public const string AdminSlug = "admin";

    private readonly ICalloraPluginCatalog _pluginCatalog =
        pluginCatalog ?? throw new ArgumentNullException(nameof(pluginCatalog));

    private readonly IPluginDeclaredPermissionCatalog _declaredPermissions =
        declaredPermissions ?? throw new ArgumentNullException(nameof(declaredPermissions));

    /// <inheritdoc />
    public async Task<IReadOnlyList<PluginRoleTemplate>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        var byPlugin = new Dictionary<string, SortedSet<string>>(StringComparer.Ordinal);

        foreach (var (pluginId, keys) in
            await _declaredPermissions.ListByPluginAsync(cancellationToken).ConfigureAwait(false))
        {
            Collect(byPlugin, pluginId, keys);
        }

        foreach (var contributor in _pluginCatalog.GetExports<IHostAdminApiExtensionContributor>())
        {
            Collect(byPlugin, contributor.PluginId, contributor.PermissionKeys);
        }

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

    private static void Collect(
        Dictionary<string, SortedSet<string>> byPlugin, string? pluginId, IEnumerable<string>? keys)
    {
        if (string.IsNullOrWhiteSpace(pluginId))
        {
            return;
        }

        var bucket = byPlugin.TryGetValue(pluginId.Trim(), out var existing)
            ? existing
            : byPlugin[pluginId.Trim()] = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var key in keys ?? [])
        {
            // Dieselbe Prüfung wie im Inventar: Einen Schlüssel zu vergeben, der nie greifen kann,
            // setzt den Betreiber genau dorthin zurück, wo er losging — nur diesmal mit einer Rolle,
            // die aussieht, als täte sie etwas.
            if (BackendPermissionKeyValidator.IsValid(key))
            {
                bucket.Add(key);
            }
        }
    }
}
