using System.Reflection;
using Callora.Core.Application.Plugins;
using Callora.Core.Application.Plugins.Contracts;

namespace Callora.Core.Application.Security;

/// <summary>
/// Jede Berechtigung, die es auf dieser Installation gibt — die des Kerns und die, die Plugins
/// mitbringen.
/// </summary>
/// <remarks>
/// Die eine Stelle, die diese Liste kennt. Vorher stand sie inline im Rollen-Endpunkt, und der
/// zweite Verwender (die Claim-Ableitung für einen SuperAdmin, ADR-023 §2) hätte sie
/// nachgebaut — mit dem üblichen Ausgang: Was die Rollenverwaltung anbietet und was auf einer
/// Fläche gilt, wäre auseinandergelaufen, sobald ein Plugin eine Berechtigung ergänzt.
/// </remarks>
public static class BackendPermissionInventory
{
    /// <summary>
    /// Alle bekannten Berechtigungsschlüssel, sortiert und ohne Dubletten.
    /// </summary>
    /// <param name="pluginCatalog">Quelle der von Plugins beigesteuerten Berechtigungen.</param>
    /// <param name="declaredByManifest">
    /// Zusätzlich die in <c>registry.json</c> deklarierten Schlüssel.
    /// </param>
    /// <remarks>
    /// Zwei Zulieferwege, ein Inventar. Über <see cref="IHostAdminApiExtensionContributor"/>
    /// konnte ein Plugin schon immer Schlüssel beisteuern — wer Admin-API-Routen beiträgt, kam
    /// damit durch. Ein Plugin, dessen Fläche aus <c>IApiController</c>-Routen besteht, hatte
    /// diesen Weg nicht: Seine Routen VERLANGTEN einen Schlüssel, den niemand vergeben konnte.
    /// Absicherung wirksam, Vergabe unmöglich — dieselbe Fehlerklasse, die schon einmal in
    /// <see cref="BackendPermissionKeyValidator"/> behoben wurde. Welchen Weg ein Plugin nutzt,
    /// muss der Betreiber nicht wissen.
    /// </remarks>
    public static IReadOnlyList<string> All(
        ICalloraPluginCatalog pluginCatalog,
        IEnumerable<string>? declaredByManifest = null)
    {
        ArgumentNullException.ThrowIfNull(pluginCatalog);

        var pluginPermissions = pluginCatalog
            .GetExports<IHostAdminApiExtensionContributor>()
            .SelectMany(contributor => contributor.PermissionKeys);

        return Core()
            .Concat(pluginPermissions)
            .Concat(declaredByManifest ?? [])
            // Auch für die deklarierten Schlüssel: Das Manifest weist strukturell ungültige
            // schon ab, aber gefüttert wird dieses Inventar auch von älteren Installationen,
            // deren Manifest vor dieser Prüfung entstand. Einen Schlüssel anzubieten, der nie
            // greifen kann, setzte den Betreiber genau dorthin zurück, wo er losging.
            .Where(BackendPermissionKeyValidator.IsValid)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>
    /// Die Berechtigungen des Kerns, aus <see cref="BackendPermissionKeys"/> gelesen.
    /// </summary>
    /// <remarks>
    /// Über Reflection statt einer gepflegten Liste: Eine zweite Aufzählung derselben Konstanten
    /// wäre genau der Ort, an dem eine neue Berechtigung fehlt — abgesichert wäre der Endpunkt
    /// dann trotzdem, vergeben ließe sie sich nicht.
    /// </remarks>
    public static IReadOnlyList<string> Core() =>
        typeof(BackendPermissionKeys)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.FieldType == typeof(string))
            .Select(field => field.GetValue(null) as string)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .ToArray();
}
