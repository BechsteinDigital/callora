using Callora.Core.Application.Plugins;
using Callora.Core.Application.Plugins.Contracts;

namespace Callora.Core.Application.Security;

/// <inheritdoc />
public sealed class PluginPermissionMap(
    ICalloraPluginCatalog pluginCatalog,
    IPluginDeclaredPermissionCatalog declaredPermissions) : IPluginPermissionMap
{
    private readonly ICalloraPluginCatalog _pluginCatalog =
        pluginCatalog ?? throw new ArgumentNullException(nameof(pluginCatalog));

    private readonly IPluginDeclaredPermissionCatalog _declaredPermissions =
        declaredPermissions ?? throw new ArgumentNullException(nameof(declaredPermissions));

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> ByPluginAsync(
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

        return byPlugin
            .Where(entry => entry.Value.Count > 0)
            .ToDictionary(
                entry => entry.Key,
                entry => (IReadOnlyList<string>)[.. entry.Value],
                StringComparer.Ordinal);
    }

    private static void Collect(
        Dictionary<string, SortedSet<string>> byPlugin, string? pluginId, IEnumerable<string>? keys)
    {
        if (string.IsNullOrWhiteSpace(pluginId))
        {
            return;
        }

        var owner = pluginId.Trim();
        var bucket = byPlugin.TryGetValue(owner, out var existing)
            ? existing
            : byPlugin[owner] = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var key in keys ?? [])
        {
            // Zwei Prüfungen, und beide sind hier nötig.
            //
            // Die Struktur, weil ein Schlüssel ohne Punkt zur Laufzeit nie greift — dieselbe Prüfung
            // wie im Inventar, damit nicht das eine anbietet, was das andere verschweigt.
            //
            // Der Namensraum, weil diese Zuordnung inzwischen entscheidet, was in einer Sitzung landet.
            // Das Manifest weist einen fremden Schlüssel längst ab; der Contributor-Weg hatte diese
            // Grenze nie, und ohne sie könnte ein Plugin über seine beigesteuerten Schlüssel
            // "user.delete" in die Sitzung eines Workspace-Admins schreiben — eine Berechtigung, die
            // über den Workspace hinausreicht, aus einem Plugin heraus, das in ihm aktiv ist.
            //
            // Bewusst NICHT die Aktionsliste des Manifests: composer.layout.publish und
            // communication.accounts.manage sind heute in Betrieb und fielen darunter weg.
            if (BackendPermissionKeyValidator.IsValid(key)
                && PluginPermissionKeyPolicy.IsInsideNamespace(owner, key, out _))
            {
                bucket.Add(key.Trim());
            }
        }
    }
}
