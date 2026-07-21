namespace Callora.Plugin.Communication.Abstractions;

/// <summary>
/// Betriebsverfügbarkeit einer SIP-Line, abgeleitet aus Account-Status, dem
/// Enabled-Flag und der aktuellen Call-Belegung. Read-only für Konsumenten.
/// </summary>
public enum SipLineStatus
{
    /// <summary>Line deaktiviert.</summary>
    Disabled = 0,

    /// <summary>Nicht verfügbar (Account nicht verbunden).</summary>
    Unavailable = 1,

    /// <summary>Verfügbar für ein-/ausgehende Calls.</summary>
    Available = 2,

    /// <summary>Belegt (aktueller Call bzw. Concurrency-Limit erreicht).</summary>
    Busy = 3
}
