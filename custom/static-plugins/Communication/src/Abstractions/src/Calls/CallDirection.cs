namespace Callora.Plugin.Communication.Abstractions;

/// <summary>
/// Direction of one call relative to the platform.
/// </summary>
public enum CallDirection
{
    /// <summary>The call was initiated by the platform.</summary>
    Outbound = 0,

    /// <summary>The call was received by the platform.</summary>
    Inbound = 1,
}
