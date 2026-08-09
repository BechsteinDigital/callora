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
    /// <param name="pluginCatalog">Quelle der von Plugins deklarierten Berechtigungen.</param>
    public static IReadOnlyList<string> All(ICalloraPluginCatalog pluginCatalog)
    {
        ArgumentNullException.ThrowIfNull(pluginCatalog);

        var pluginPermissions = pluginCatalog
            .GetExports<IHostAdminApiExtensionContributor>()
            .SelectMany(contributor => contributor.PermissionKeys)
            .Where(BackendPermissionKeyValidator.IsValid);

        return Core()
            .Concat(pluginPermissions)
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
