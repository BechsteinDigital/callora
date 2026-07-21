namespace Callora.Plugin.Communication.Abstractions;

/// <summary>
/// Konnektivitätsstatus eines SIP-Accounts: Registrierungszustand (Register-Modus)
/// bzw. Trunk-Erreichbarkeit (Trunk-Modus). Read-only für Konsumenten.
/// </summary>
public enum SipAccountStatus
{
    /// <summary>Account deaktiviert.</summary>
    Disabled = 0,

    /// <summary>Verbindung/Registrierung läuft an.</summary>
    Connecting = 1,

    /// <summary>Registriert bzw. Trunk erreichbar.</summary>
    Up = 2,

    /// <summary>Eingeschränkt (z. B. instabile Registrierung).</summary>
    Degraded = 3,

    /// <summary>Registrierung/Erreichbarkeit fehlgeschlagen.</summary>
    Failed = 4
}
