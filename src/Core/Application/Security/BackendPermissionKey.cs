namespace Callora.Core.Application.Security;

/// <summary>
/// Ein Berechtigungsschlüssel, zerlegt in Funktion und Aktion.
/// </summary>
/// <remarks>
/// Die eine Stelle, an der ein Schlüssel gelesen wird. Vorher waren es drei, mit drei
/// Ergebnissen: Die Gültigkeitsprüfung verlangte genau zwei Segmente aus einem festen
/// Aktions-Vokabular, die Anzeige teilte am ERSTEN Punkt, und die Autorisierung verglich die
/// ganze Zeichenkette.
///
/// <para>
/// Die Folge war die teuerste Kombination, die es gibt: Die Absicherung wirkte, die Vergabe
/// nicht. <c>communication.accounts.read</c> hat drei Segmente und fiel damit aus dem Katalog —
/// der Endpunkt verlangte die Berechtigung trotzdem, aber kein Operator konnte sie einer Rolle
/// geben. Für jedes Plugin, das eigene Berechtigungen mitbringt, also für alle.
/// </para>
///
/// <para>
/// <b>Die Aktion ist das LETZTE Segment</b>, die Funktion alles davor. So bleibt der Punkt der
/// Namensraumtrenner, den Plugins ohnehin benutzen: <c>communication.accounts</c> ·
/// <c>read</c>.
/// </para>
/// </remarks>
public readonly record struct BackendPermissionKey(string Function, string Action)
{
    /// <summary>
    /// Zerlegt einen Schlüssel. Geprüft wird die STRUKTUR, nicht das Vokabular.
    /// </summary>
    /// <remarks>
    /// Eine geschlossene Aktionsliste im Kern hätte jeden Plugin-Autor auf die Wörter
    /// festgelegt, die der Kern für seine eigenen Endpunkte gewählt hat — und ein
    /// <c>publish</c> oder <c>manage</c> nicht etwa abgelehnt, sondern unsichtbar gemacht. Wer
    /// Wildwuchs vermeiden will, tut das in einer Doku oder einem Analyzer, nicht in einem
    /// Filter, der die Vergabe stillschweigend verschluckt.
    /// </remarks>
    public static bool TryParse(string? permissionKey, out BackendPermissionKey key)
    {
        key = default;
        if (string.IsNullOrWhiteSpace(permissionKey))
        {
            return false;
        }

        var trimmed = permissionKey.Trim();
        var separator = trimmed.LastIndexOf('.');
        if (separator <= 0 || separator == trimmed.Length - 1)
        {
            return false;
        }

        var function = trimmed[..separator].Trim();
        var action = trimmed[(separator + 1)..].Trim();
        if (function.Length == 0 || action.Length == 0 || function.EndsWith('.') || function.Contains(".."))
        {
            return false;
        }

        key = new BackendPermissionKey(function, action);
        return true;
    }
}
