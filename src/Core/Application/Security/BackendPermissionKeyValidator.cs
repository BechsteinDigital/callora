using Callora.Core.Extensibility;

namespace Callora.Core.Application.Security;

/// <summary>
/// Validates permission keys following &lt;function&gt;.&lt;action&gt; schema.
/// </summary>
[CalloraInternal("Permission-key validation — not a plugin contract (REV2 §7.2)")]
public static class BackendPermissionKeyValidator
{
    /// <remarks>
    /// Prüft die STRUKTUR über <see cref="BackendPermissionKey.TryParse"/>, nicht mehr das
    /// Vokabular. Vorher verlangte diese Methode genau zwei Segmente aus einer festen
    /// Aktionsliste — und verschluckte damit jede Plugin-Berechtigung
    /// (<c>communication.accounts.read</c>, <c>composer.layout.publish</c>) aus dem Katalog,
    /// während die Endpunkte sie weiter verlangten. Absicherung wirksam, Vergabe unmöglich.
    /// </remarks>
    public static bool IsValid(string permissionKey) =>
        BackendPermissionKey.TryParse(permissionKey, out _);
}
