namespace Callora.Plugin.Communication.Abstractions;

/// <summary>
/// Aggregierter Gesundheitszustand eines Channels, abgeleitet aus dem
/// Verbindungsstatus seiner Accounts — read-only für Konsumenten.
/// </summary>
public enum ChannelHealth
{
    /// <summary>Zustand noch unbekannt (kein Account angebunden/geprüft).</summary>
    Unknown = 0,

    /// <summary>Mindestens ein Account ist verbunden; Calls möglich.</summary>
    Up = 1,

    /// <summary>Teilweise verbunden (einzelne Accounts/Lines fehlerhaft).</summary>
    Degraded = 2,

    /// <summary>Kein Account verbunden; keine Calls möglich.</summary>
    Down = 3
}
